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
            if (outputDir == null)
            {
                outputDir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "SteamUpload");
            }
            
            Directory.CreateDirectory(outputDir);
            
            var sb = new StringBuilder();
            
            sb.AppendLine("\"AppBuild\"");
            sb.AppendLine("{");
            sb.AppendLine($"\t\"AppID\" \"{config.appId}\"");
            sb.AppendLine($"\t\"Desc\" \"{EscapeVDF(description)}\"");
            sb.AppendLine($"\t\"ContentRoot\" \"{EscapePath(Path.GetDirectoryName(Application.dataPath))}\"");
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
                    var depotVdfPath = GenerateDepotBuild(depot, outputDir);
                    sb.AppendLine($"\t\t\"{depot.depotId}\" \"{EscapePath(depotVdfPath)}\"");
                }
            }
            
            sb.AppendLine("\t}");
            sb.AppendLine("}");
            
            var appBuildPath = Path.Combine(outputDir, $"app_build_{config.appId}.vdf");
            File.WriteAllText(appBuildPath, sb.ToString());
            
            return appBuildPath;
        }

        /// <summary>
        /// Сгенерировать depot_build.vdf для одного депо
        /// </summary>
        public static string GenerateDepotBuild(DepotEntry depot, string outputDir)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("\"DepotBuild\"");
            sb.AppendLine("{");
            sb.AppendLine($"\t\"DepotID\" \"{depot.depotId}\"");
            sb.AppendLine($"\t\"ContentRoot\" \"{EscapePath(depot.buildPath)}\"");
            
            // FileMapping
            sb.AppendLine("\t\"FileMapping\"");
            sb.AppendLine("\t{");
            sb.AppendLine("\t\t\"LocalPath\" \"*\"");
            sb.AppendLine("\t\t\"DepotPath\" \".\"");
            sb.AppendLine("\t\t\"Recursive\" \"1\"");
            sb.AppendLine("\t}");
            
            // FileExclusion
            if (depot.excludePatterns != null && depot.excludePatterns.Length > 0)
            {
                foreach (var pattern in depot.excludePatterns)
                {
                    if (!string.IsNullOrEmpty(pattern))
                    {
                        sb.AppendLine($"\t\"FileExclusion\" \"{pattern}\"");
                    }
                }
            }
            
            sb.AppendLine("}");
            
            var depotPath = Path.Combine(outputDir, $"depot_build_{depot.depotId}.vdf");
            File.WriteAllText(depotPath, sb.ToString());
            
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
        /// Экранировать путь для VDF
        /// </summary>
        private static string EscapePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            // VDF требует прямые слеши
            return path.Replace("\\", "/");
        }

        /// <summary>
        /// Сгенерировать простой VDF для одного депо (упрощённый вариант)
        /// </summary>
        public static string GenerateSimpleAppBuild(string appId, string depotId, string contentPath, 
            string branch, string description, bool preview = false)
        {
            var outputDir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "SteamUpload");
            Directory.CreateDirectory(outputDir);
            
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
            File.WriteAllText(appBuildPath, sb.ToString());
            
            return appBuildPath;
        }
    }
}
