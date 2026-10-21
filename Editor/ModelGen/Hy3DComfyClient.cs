using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ProtoSystem.ModelGen.Editor
{
    /// <summary>
    /// Клиент ComfyUI для Hunyuan3D 2.1: аплоад референса, строковый граф API,
    /// поллинг /history, чтение готового GLB из локальной папки output.
    /// Граф проверен смоук-тестом (июль 2026), ~3.5 мин на RTX 5060 Ti.
    /// </summary>
    public static class Hy3DComfyClient
    {
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(60) };

        public static async Task<bool> IsServerOnline(string server)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                var resp = await Http.GetAsync(server.TrimEnd('/') + "/system_stats", cts.Token);
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public static async Task<ModelGenResult> GenerateAsync(ModelGenRequest req,
            string server, string outputDir, Action<float, string> progress, CancellationToken ct)
        {
            var result = new ModelGenResult { Request = req };
            server = server.TrimEnd('/');
            try
            {
                if (string.IsNullOrEmpty(req.RefImagePath) || !File.Exists(req.RefImagePath))
                {
                    result.Error = "Hunyuan3D работает только по референс-картинке (image-to-3D). Укажи референс.";
                    return result;
                }

                progress?.Invoke(0.02f, "аплоад референса");
                string uploaded = await UploadImageAsync(server, req.RefImagePath, ct);

                string meshName = $"ms_{Sanitize(req.EntityId)}_{DateTime.Now:yyyyMMdd_HHmmss}";
                string graph = BuildGraph(req, uploaded, meshName);

                progress?.Invoke(0.05f, "постановка в очередь");
                var submit = await Http.PostAsync(server + "/prompt",
                    new StringContent("{\"prompt\":" + graph + "}", Encoding.UTF8, "application/json"), ct);
                string submitBody = await submit.Content.ReadAsStringAsync();
                if (!submit.IsSuccessStatusCode)
                {
                    result.Error = "ComfyUI отклонил граф: " + Truncate(submitBody, 500);
                    return result;
                }
                string promptId = ExtractJsonString(submitBody, "prompt_id");
                if (string.IsNullOrEmpty(promptId)) { result.Error = "Нет prompt_id в ответе ComfyUI."; return result; }

                // Поллинг истории
                var started = DateTime.UtcNow;
                while (true)
                {
                    ct.ThrowIfCancellationRequested();
                    await Task.Delay(4000, ct);
                    var histResp = await Http.GetAsync(server + "/history/" + promptId, ct);
                    string hist = await histResp.Content.ReadAsStringAsync();
                    if (hist.Length > 5 && hist.Contains(promptId))
                    {
                        if (hist.Contains("\"status_str\": \"error\"") || hist.Contains("\"status_str\":\"error\""))
                        {
                            result.Error = "Ошибка узла: " + Truncate(ExtractJsonString(hist, "exception_message") ?? hist, 500);
                            return result;
                        }
                        if (hist.Contains("\"completed\": true") || hist.Contains("\"completed\":true"))
                            break;
                    }
                    float t = (float)(DateTime.UtcNow - started).TotalSeconds;
                    progress?.Invoke(Mathf.Clamp01(0.05f + t / 260f * 0.85f), "генерация (меш → текстуры)");
                    if (t > 1800) { result.Error = "Таймаут генерации (30 мин)."; return result; }
                }

                // GLB пишет нода InPaint в output ComfyUI
                progress?.Invoke(0.95f, "чтение GLB");
                string glbPath = Path.Combine(outputDir, meshName + ".glb");
                for (int i = 0; i < 10 && !File.Exists(glbPath); i++)
                    await Task.Delay(1000, ct);
                if (!File.Exists(glbPath))
                {
                    result.Error = "GLB не найден: " + glbPath + " (проверь папку output в настройках).";
                    return result;
                }
                result.GlbBytes = File.ReadAllBytes(glbPath);
                return result;
            }
            catch (OperationCanceledException) { result.Error = "Отменено."; return result; }
            catch (Exception e) { result.Error = e.Message; return result; }
        }

        private static async Task<string> UploadImageAsync(string server, string path, CancellationToken ct)
        {
            using var form = new MultipartFormDataContent();
            var bytes = File.ReadAllBytes(path);
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            string name = "ms_ref_" + Path.GetFileName(path);
            form.Add(file, "image", name);
            form.Add(new StringContent("true"), "overwrite");
            var resp = await Http.PostAsync(server + "/upload/image", form, ct);
            string body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) throw new Exception("Аплоад референса не удался: " + Truncate(body, 300));
            return ExtractJsonString(body, "name") ?? name;
        }

        /// <summary>Полный граф: меш (DIT+VAE) → постпроцесс → мультивью PBR → запекание → GLB.</summary>
        private static string BuildGraph(ModelGenRequest req, string imageName, string meshName)
        {
            string dit = ModelAiSettings.DitModel;
            string vae = ModelAiSettings.VaeModel;
            var sb = new StringBuilder(4096);
            sb.Append('{');
            sb.Append("\"1\":{\"class_type\":\"Hy3D21LoadImageWithTransparency\",\"inputs\":{\"image\":").J(imageName).Append("}},");
            sb.Append("\"2\":{\"class_type\":\"Hy3DMeshGenerator\",\"inputs\":{\"model\":").J(dit)
              .Append(",\"image\":[\"1\",2],\"steps\":").Append(req.Steps)
              .Append(",\"guidance_scale\":").Append(req.Guidance.ToString(System.Globalization.CultureInfo.InvariantCulture))
              .Append(",\"seed\":").Append(req.Seed).Append(",\"attention_mode\":\"sdpa\"}},");
            sb.Append("\"3\":{\"class_type\":\"Hy3D21VAELoader\",\"inputs\":{\"model_name\":").J(vae).Append("}},");
            sb.Append("\"4\":{\"class_type\":\"Hy3D21VAEDecode\",\"inputs\":{\"vae\":[\"3\",0],\"latents\":[\"2\",0],\"box_v\":1.01,\"octree_resolution\":")
              .Append(req.OctreeRes).Append(",\"num_chunks\":8000,\"mc_level\":0.0,\"mc_algo\":\"mc\",\"enable_flash_vdm\":false,\"force_offload\":true}},");
            sb.Append("\"5\":{\"class_type\":\"Hy3D21PostprocessMesh\",\"inputs\":{\"trimesh\":[\"4\",0],\"remove_floaters\":true,\"remove_degenerate_faces\":true,\"reduce_faces\":true,\"max_facenum\":")
              .Append(req.MaxFaces).Append(",\"smooth_normals\":false}},");
            sb.Append("\"6\":{\"class_type\":\"Hy3D21CameraConfig\",\"inputs\":{\"camera_azimuths\":\"0, 90, 180, 270, 0, 180\",\"camera_elevations\":\"0, 0, 0, 0, 90, -90\",\"view_weights\":\"1, 0.1, 0.5, 0.1, 0.05, 0.05\",\"ortho_scale\":1.1}},");
            sb.Append("\"7\":{\"class_type\":\"Hy3DMultiViewsGenerator\",\"inputs\":{\"trimesh\":[\"5\",0],\"camera_config\":[\"6\",0],\"view_size\":")
              .Append(req.ViewSize).Append(",\"image\":[\"1\",2],\"steps\":").Append(req.PaintSteps)
              .Append(",\"guidance_scale\":3.0,\"texture_size\":").Append(req.TextureSize)
              .Append(",\"unwrap_mesh\":true,\"seed\":").Append(req.Seed).Append("}},");
            sb.Append("\"8\":{\"class_type\":\"Hy3DBakeMultiViews\",\"inputs\":{\"pipeline\":[\"7\",0],\"camera_config\":[\"7\",5],\"albedo\":[\"7\",1],\"mr\":[\"7\",2]}},");
            sb.Append("\"9\":{\"class_type\":\"Hy3DInPaint\",\"inputs\":{\"pipeline\":[\"8\",0],\"albedo\":[\"8\",1],\"albedo_mask\":[\"8\",2],\"mr\":[\"8\",3],\"mr_mask\":[\"8\",4],\"output_mesh_name\":")
              .J(meshName).Append("}}");
            sb.Append('}');
            return sb.ToString();
        }

        private static StringBuilder J(this StringBuilder sb, string s)
            => sb.Append('"').Append(s.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');

        private static string Sanitize(string s)
        {
            var sb = new StringBuilder();
            foreach (var c in s) sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            return sb.ToString();
        }

        private static string Truncate(string s, int n) => string.IsNullOrEmpty(s) || s.Length <= n ? s : s[..n];

        /// <summary>Мини-парсер: первое строковое значение по ключу.</summary>
        internal static string ExtractJsonString(string json, string key)
        {
            int i = json.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            if (i < 0) return null;
            i = json.IndexOf(':', i);
            if (i < 0) return null;
            i = json.IndexOf('"', i);
            if (i < 0) return null;
            int end = i + 1;
            var sb = new StringBuilder();
            while (end < json.Length && json[end] != '"')
            {
                if (json[end] == '\\' && end + 1 < json.Length) { sb.Append(json[end + 1]); end += 2; }
                else sb.Append(json[end++]);
            }
            return sb.ToString();
        }
    }
}
