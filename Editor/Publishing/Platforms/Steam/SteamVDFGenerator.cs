// Packages/com.protosystem.core/Editor/Publishing/Platforms/Steam/SteamVDFGenerator.cs
using System.IO;
using System.Text;
using UnityEngine;

namespace ProtoSystem.Publishing.Editor
{
    /// <summary>
    /// Генератор VDF файлов для SteamCMD
    /// </summary>
    public static class SteamVDFGenerator
    {
        /// <summary>
        /// Сгенерировать app_build.vdf
        /// </summary>
        public static string GenerateAppBuild(SteamConfig config, string branch, string description, 
            string outputDir = null)
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            
            if (outputDir == null)
            {
                outputDir = Path.Combine(projectRoot, "SteamUpload");
            }
            
            Directory.CreateDirectory(outputDir);
            Directory.CreateDirectory(Path.Combine(outputDir, "output"));
            
            var sb = new StringBuilder();
            
            sb.AppendLine("\"AppBuild\"");
            sb.AppendLine("{");
            sb.AppendLine($"\t\"AppID\" \"{config.appId}\"");
            sb.AppendLine($"\t\"Desc\" \"{EscapeVDF(description)}\"");
            sb.AppendLine($"\t\"BuildOutput\" \"{EscapePath(Path.Combine(outputDir, "output"))}\"");
            
            if (config.previewMode)
            {
                sb.AppendLine("\t\"Preview\" \"1\"");
            }
            
            if (config.autoSetLive && !string.IsNullOrEmpty(branch))
            {
                sb.AppendLine($"\t\"SetLive\" \"{branch}\"");
            }
            
            sb.AppendLine("\t\"Depots\"");
            sb.AppendLine("\t{");
            
            // Генерируем депо
            if (config.depotConfig != null)
            {
                foreach (var depot in config.depotConfig.GetEnabledDepots())
                {
                    var depotVdfPath = GenerateDepotBuild(depot, outputDir, projectRoot);
                    sb.AppendLine($"\t\t\"{depot.depotId}\" \"{EscapePath(depotVdfPath)}\"");
                }
            }
            
            sb.AppendLine("\t}");
            sb.AppendLine("}");
            
            var appBuildPath = Path.Combine(outputDir, $"app_build_{config.appId}.vdf");
            File.WriteAllText(appBuildPath, sb.ToString(), Encoding.UTF8);
            
            Debug.Log($"[Steam VDF] Generated app_build:\n{sb}");
            
            return appBuildPath;
        }

        /// <summary>
        /// Сгенерировать depot_build.vdf для одного депо
        /// </summary>
        public static string GenerateDepotBuild(DepotEntry depot, string outputDir, string projectRoot)
        {
            var sb = new StringBuilder();
            
            // Конвертируем относительный путь в абсолютный
            var contentRoot = depot.buildPath;
            if (!Path.IsPathRooted(contentRoot))
            {
                contentRoot = Path.Combine(projectRoot, depot.buildPath);
            }
            contentRoot = Path.GetFullPath(contentRoot);
            
            sb.AppendLine("\"DepotBuild\"");
            sb.AppendLine("{");
            sb.AppendLine($"\t\"DepotID\" \"{depot.depotId}\"");
            sb.AppendLine($"\t\"ContentRoot\" \"{EscapePath(contentRoot)}\"");
            
            // FileMapping
            sb.AppendLine("\t\"FileMapping\"");
            sb.AppendLine("\t{");
            sb.AppendLine("\t\t\"LocalPath\" \"*\"");
            sb.AppendLine("\t\t\"DepotPath\" \".\"");
            sb.AppendLine("\t\t\"Recursive\" \"1\"");
            sb.AppendLine("\t}");
            
            // FileExclusion
            var excludePatterns = depot.GetExcludePatterns();
            if (excludePatterns != null && excludePatterns.Length > 0)
            {
                foreach (var pattern in excludePatterns)
                {
                    if (!string.IsNullOrEmpty(pattern))
                    {
                        sb.AppendLine($"\t\"FileExclusion\" \"{EscapeVDF(pattern)}\"");
                    }
                }
            }
            
            sb.AppendLine("}");
            
            var depotPath = Path.Combine(outputDir, $"depot_build_{depot.depotId}.vdf");
            File.WriteAllText(depotPath, sb.ToString(), Encoding.UTF8);
            
            Debug.Log($"[Steam VDF] Generated depot_build for {depot.displayName}:\n{sb}");
            
            return depotPath;
        }

        /// <summary>
        /// Экранировать строку для VDF
        /// </summary>
        private static string EscapeVDF(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "");
        }

        /// <summary>
        /// Экранировать путь для VDF (SteamCMD понимает оба типа слешей, но прямые безопаснее)
        /// </summary>
        private static string EscapePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            // Нормализуем путь
            path = Path.GetFullPath(path);
            // VDF лучше работает с прямыми слешами
            return path.Replace("\\", "/");
        }

        /// <summary>
        /// Сгенерировать простой VDF для одного депо (упрощённый вариант)
        /// </summary>
        public static string GenerateSimpleAppBuild(string appId, string depotId, string contentPath, 
            string branch, string description, bool preview = false)
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var outputDir = Path.Combine(projectRoot, "SteamUpload");
            Directory.CreateDirectory(outputDir);
            Directory.CreateDirectory(Path.Combine(outputDir, "output"));
            
            // Конвертируем путь контента в абсолютный
            if (!Path.IsPathRooted(contentPath))
            {
                contentPath = Path.Combine(projectRoot, contentPath);
            }
            contentPath = Path.GetFullPath(contentPath);
            
            var sb = new StringBuilder();
            
            sb.AppendLine("\"AppBuild\"");
            sb.AppendLine("{");
            sb.AppendLine($"\t\"AppID\" \"{appId}\"");
            sb.AppendLine($"\t\"Desc\" \"{EscapeVDF(description)}\"");
            sb.AppendLine($"\t\"ContentRoot\" \"{EscapePath(contentPath)}\"");
            sb.AppendLine($"\t\"BuildOutput\" \"{EscapePath(Path.Combine(outputDir, "output"))}\"");
            
            if (preview)
            {
                sb.AppendLine("\t\"Preview\" \"1\"");
            }
            
            if (!string.IsNullOrEmpty(branch) && branch != "default")
            {
                sb.AppendLine($"\t\"SetLive\" \"{branch}\"");
            }
            
            sb.AppendLine("\t\"Depots\"");
            sb.AppendLine("\t{");
            sb.AppendLine($"\t\t\"{depotId}\"");
            sb.AppendLine("\t\t{");
            sb.AppendLine("\t\t\t\"FileMapping\"");
            sb.AppendLine("\t\t\t{");
            sb.AppendLine("\t\t\t\t\"LocalPath\" \"*\"");
            sb.AppendLine("\t\t\t\t\"DepotPath\" \".\"");
            sb.AppendLine("\t\t\t\t\"Recursive\" \"1\"");
            sb.AppendLine("\t\t\t}");
            sb.AppendLine("\t\t\t\"FileExclusion\" \"*.pdb\"");
            sb.AppendLine("\t\t}");
            sb.AppendLine("\t}");
            sb.AppendLine("}");
            
            var appBuildPath = Path.Combine(outputDir, $"app_build_{appId}.vdf");
            File.WriteAllText(appBuildPath, sb.ToString(), Encoding.UTF8);
            
            return appBuildPath;
        }
    }
}
