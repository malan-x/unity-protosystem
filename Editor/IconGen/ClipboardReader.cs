using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ProtoSystem.IconGen.Editor
{
    /// <summary>
    /// Чтение КАРТИНКИ из системного буфера обмена в PNG-файл.
    ///
    /// Почему так сложно. У Unity нет API для картинки в буфере, поэтому её достаёт
    /// PowerShell через System.Windows.Forms. И вот там засада: Clipboard.GetImage()
    /// отдаёт картинку БЕЗ АЛЬФЫ — прозрачные пиксели превращаются в чёрные. Это и
    /// есть знаменитый «вставил — фон чёрный»: виноват не импорт и не Unity, а один
    /// вызов в .NET, который роняет альфа-канал.
    ///
    /// Поэтому источники пробуются по убыванию честности:
    ///   1. формат PNG — приложение само положило готовый PNG, альфа как есть;
    ///   2. CF_DIBV5 / DIB — 32-битный растр, альфа в нём ЕСТЬ, просто GetImage её
    ///      игнорирует; читаем заголовок и собираем картинку сами;
    ///   3. GetImage() — последняя надежда (Paint, старые окна), альфы не будет,
    ///      и об этом честно предупреждаем.
    ///
    /// Возвращается флаг flat: картинка пришла без прозрачности, и звать «убрать фон»
    /// придётся руками.
    /// </summary>
    public static class ClipboardReader
    {
        /// <summary>Результат попытки достать картинку.</summary>
        public enum Result
        {
            None,       // в буфере картинки нет
            Png,        // PNG из буфера — альфа родная
            Dib,        // собрана из 32-битного DIB — альфа восстановлена
            Flat,       // GetImage() — прозрачности нет
            Failed,     // не удалось запустить PowerShell
        }

        public static bool IsImage(Result r) => r == Result.Png || r == Result.Dib || r == Result.Flat;

        /// <summary>
        /// Записать картинку из буфера в absolutePath (.png). Каталог должен существовать.
        /// </summary>
        public static Result WriteImage(string absolutePath)
        {
            if (Application.platform != RuntimePlatform.WindowsEditor) return Result.None;

            string scriptPath = Path.Combine(Path.GetTempPath(), "protosystem_clipboard_read.ps1");
            try
            {
                // Скрипт кладём файлом, а не строкой в -Command: разбор DIB не влезает
                // в одну строку без ада экранирования, а кавычки внутри -Command
                // ломаются об кавычки самого процесса
                File.WriteAllText(scriptPath, Script, new System.Text.UTF8Encoding(false));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Буфер] Не удалось записать скрипт чтения: {e.Message}");
                return Result.Failed;
            }

            string output;
            try
            {
                var info = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    // STA обязателен: буфер обмена Windows недоступен из MTA-потока
                    Arguments = $"-NoProfile -STA -NonInteractive -ExecutionPolicy Bypass " +
                                $"-File \"{scriptPath}\" -Out \"{absolutePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                using var process = Process.Start(info);
                if (process == null) return Result.Failed;

                output = process.StandardOutput.ReadToEnd().Trim();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit(10000);

                if (!string.IsNullOrWhiteSpace(error))
                    Debug.LogWarning($"[Буфер] PowerShell: {error.Trim()}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Буфер] Не удалось прочитать буфер обмена: {e.Message}");
                return Result.Failed;
            }

            if (!File.Exists(absolutePath)) return Result.None;

            if (output.Contains("OK-PNG")) return Result.Png;
            if (output.Contains("OK-DIB")) return Result.Dib;
            if (output.Contains("OK-FLAT"))
            {
                Debug.LogWarning("[Буфер] Приложение отдало картинку без альфа-канала — " +
                                 "прозрачный фон станет сплошным. Скопируйте PNG-файл " +
                                 "или уберите фон кнопкой в окне подгонки.");
                return Result.Flat;
            }
            return Result.None;
        }

        // Скрипт нарочно многословный: он единственное место, где мы разбираем DIB,
        // и через полгода его будут читать, а не переписывать
        private const string Script = @"
param([string]$Out)
Add-Type -AssemblyName System.Windows.Forms,System.Drawing

$data = [System.Windows.Forms.Clipboard]::GetDataObject()
if ($data -eq $null) { Write-Output 'NOIMAGE'; exit 1 }

# 1. Готовый PNG: альфа уже внутри, просто сохраняем поток
foreach ($fmt in @('PNG','image/png')) {
    if ($data.GetDataPresent($fmt)) {
        $stream = $data.GetData($fmt)
        if ($stream -ne $null) {
            $fs = [System.IO.File]::Create($Out)
            $stream.Position = 0
            $stream.CopyTo($fs)
            $fs.Close()
            Write-Output 'OK-PNG'
            exit 0
        }
    }
}

# 2. DIBV5 / DIB: 32-битный растр, альфа в нём есть — GetImage её теряет
foreach ($fmt in @('Format17','DeviceIndependentBitmap')) {
    if (-not $data.GetDataPresent($fmt)) { continue }
    $ms = $data.GetData($fmt)
    if ($ms -eq $null) { continue }

    $bytes = New-Object byte[] $ms.Length
    $ms.Position = 0
    [void]$ms.Read($bytes, 0, $bytes.Length)
    if ($bytes.Length -lt 40) { continue }

    $headerSize  = [System.BitConverter]::ToInt32($bytes, 0)
    $width       = [System.BitConverter]::ToInt32($bytes, 4)
    $rawHeight   = [System.BitConverter]::ToInt32($bytes, 8)
    $bitCount    = [System.BitConverter]::ToInt16($bytes, 14)
    $compression = [System.BitConverter]::ToInt32($bytes, 16)

    # 16- и 24-битные DIB прозрачности не несут — им место в ветке GetImage
    if ($bitCount -ne 32) { continue }
    if ($width -le 0) { continue }

    $height = [Math]::Abs($rawHeight)
    $bottomUp = $rawHeight -gt 0        # положительная высота = строки снизу вверх

    $offset = $headerSize
    # BI_BITFIELDS у 40-байтового заголовка добавляет три маски по 4 байта
    if ($headerSize -eq 40 -and $compression -eq 3) { $offset += 12 }
    $stride = $width * 4
    if ($bytes.Length -lt $offset + $stride * $height) { continue }

    # Некоторые приложения кладут 32 бита с НУЛЕВОЙ альфой везде. Взять её как есть —
    # получить полностью невидимую картинку, что хуже чёрного фона
    $anyAlpha = $false
    for ($i = $offset + 3; $i -lt $bytes.Length; $i += 4) {
        if ($bytes[$i] -ne 0) { $anyAlpha = $true; break }
    }

    $bmp = New-Object System.Drawing.Bitmap($width, $height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $rect = New-Object System.Drawing.Rectangle(0, 0, $width, $height)
    $locked = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::WriteOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

    $row = New-Object byte[] $stride
    for ($y = 0; $y -lt $height; $y++) {
        $src = $offset + $stride * $(if ($bottomUp) { $height - 1 - $y } else { $y })
        [System.Array]::Copy($bytes, $src, $row, 0, $stride)
        if (-not $anyAlpha) {
            for ($x = 3; $x -lt $stride; $x += 4) { $row[$x] = 255 }
        }
        $dst = [System.IntPtr]::Add($locked.Scan0, $locked.Stride * $y)
        [System.Runtime.InteropServices.Marshal]::Copy($row, 0, $dst, $stride)
    }

    $bmp.UnlockBits($locked)
    $bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Output 'OK-DIB'
    exit 0
}

# 3. Последняя надежда: альфы здесь не будет
$img = [System.Windows.Forms.Clipboard]::GetImage()
if ($img -ne $null) {
    $img.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Output 'OK-FLAT'
    exit 0
}

Write-Output 'NOIMAGE'
exit 1
";
    }
}
