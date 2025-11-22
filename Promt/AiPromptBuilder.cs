using frontAIagent.Models;
using frontAIagent.Pages;

namespace frontAIagent.Promt
{
    using System.Text;

    public class AiPromptBuilder : IAiPromptBuilder
    {
        private readonly int _maxCharsPerFile = 4000;
        private readonly int _maxTotalContextChars = 35000;
        private readonly int _maxFilesToInclude = 80;

        public AiPromptBuilder() { }

        public async Task<string> BuildPromptAsync(
            SavedProject project,
            string userMessage,
            FileReadResult fileContext,
            string projectStructure,
            string personaHint = "Ты — Senior Developer Assistant. Помогаешь писать рабочий код и даёшь живые примеры.",
            string? logs = null)
        {
            await Task.CompletedTask;

            var sb = new StringBuilder();

            // 1) SYSTEM / PERSONA
            sb.AppendLine("=== SYSTEM / PERSONA ===");
            sb.AppendLine(personaHint);
            sb.AppendLine("Правила:");
            sb.AppendLine("— Отвечай строго на русском, живо и понятно.");
            sb.AppendLine("— Пиши код прямо с файлами и путями.");
            sb.AppendLine("— Markdown-блоки для кода и diff, объясняй коротко при необходимости.");
            sb.AppendLine("— Учитывай данные файлов и логов, которые прислали.");
            sb.AppendLine();

            // 2) PROJECT METADATA
            sb.AppendLine("=== PROJECT METADATA ===");
            sb.AppendLine($"Project: {project?.AnalysisName ?? "(нет имени)"}");
            sb.AppendLine($"Id: {project?.Id ?? 0}");
            sb.AppendLine($"Path: {project?.DirectoryPath ?? "(неизвестно)"}");
            sb.AppendLine($"File filter: {project?.FileType ?? "(не указано)"}");
            sb.AppendLine();

            // 3) PROJECT STRUCTURE
            sb.AppendLine("=== PROJECT STRUCTURE ===");
            sb.AppendLine(projectStructure ?? "(нет структуры)");
            sb.AppendLine();

            // 4) FILES POLICY
            sb.AppendLine("=== FILES & CONTENT (policy) ===");
            sb.AppendLine($"Файлов найдено: {fileContext?.TotalFiles ?? 0}, прочитано: {fileContext?.SuccessFiles ?? 0}");
            sb.AppendLine($"Максимум {_maxFilesToInclude} файлов по {_maxCharsPerFile} символов.");
            sb.AppendLine($"Общий лимит контекста: {_maxTotalContextChars} символов.");
            sb.AppendLine();

            // 5) FILES CONTENT
            int totalChars = 0, included = 0;
            if (fileContext?.ProcessedFiles?.Any() == true)
            {
                sb.AppendLine("=== FILES CONTENT ===");
                var files = fileContext.ProcessedFiles.Distinct()
                    .OrderBy(GetPriorityForPath).ThenBy(f => f.Length);
                foreach (var rel in files)
                {
                    if (included >= _maxFilesToInclude || totalChars >= _maxTotalContextChars) break;
                    var content = ExtractFileContent(fileContext.CombinedContent ?? "", rel);
                    if (content == null) continue;
                    var trimmed = SanitizeForPrompt(TrimToLength(content, _maxCharsPerFile));
                    sb.AppendLine($"--- FILE: {rel} ---");
                    sb.AppendLine(trimmed);
                    sb.AppendLine();
                    included++;
                    totalChars += trimmed.Length;
                }
            }
            else sb.AppendLine("(Нет файлов для включения.)");
            sb.AppendLine();

            // 6) LOGS
            if (!string.IsNullOrWhiteSpace(logs))
            {
                sb.AppendLine("=== LOGS ===");
                sb.AppendLine(SanitizeForPrompt(TrimToLength(logs, Math.Min(8000, _maxTotalContextChars / 2))));
                sb.AppendLine();
            }

            // 7) USER REQUEST
            sb.AppendLine("=== USER REQUEST ===");
            sb.AppendLine(userMessage.Trim());
            sb.AppendLine();

            // 8) OUTPUT FORMAT
            sb.AppendLine("=== TASK / OUTPUT ===");
            sb.AppendLine("Отвечай живо, как коллега. Код вставляй в Markdown-блоки:");
            sb.AppendLine("📌 *Файл:* /path/to/file.ext");
            sb.AppendLine("```language");
            sb.AppendLine("// код здесь");
            sb.AppendLine("```");
            sb.AppendLine("Diff:");
            sb.AppendLine("```diff");
            sb.AppendLine("+ добавлено");
            sb.AppendLine("- удалено");
            sb.AppendLine("```");
            sb.AppendLine("Делай инструкции и шаги только если они реально помогают понять код.");
            sb.AppendLine("Пиши ясно, чтобы можно было скопировать и вставить код.");
            sb.AppendLine("Если информация неполная — попроси недостающие файлы.");
            sb.AppendLine("Если запрос не относится к проекту, ответь: \"Этот запрос не относится к проекту.\"");
            sb.AppendLine();

            // FINAL TRIM
            var result = sb.ToString();
            if (result.Length > _maxTotalContextChars * 2)
                result = result.Substring(0, _maxTotalContextChars * 2) + "\n\n[TRUNCATED CONTEXT]";

            return result;
        }

        private static int GetPriorityForPath(string path)
        {
            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".cs" or ".py" => 1,
                ".js" or ".ts" or ".json" => 2,
                ".xml" or ".config" => 3,
                ".md" => 4,
                ".txt" => 5,
                ".log" => 6,
                _ => 10
            };
        }

        private static string? ExtractFileContent(string combined, string relativePath)
        {
            if (string.IsNullOrEmpty(combined)) return null;
            var marker = $"----------------{relativePath}----------------";
            var idx = combined.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0) return null;
            idx += marker.Length;
            var nextIdx = combined.IndexOf("----------------", idx, StringComparison.Ordinal);
            if (nextIdx < 0) nextIdx = combined.Length;
            return combined.Substring(idx, nextIdx - idx).Trim();
        }

        private string TrimToLength(string text, int max)
        {
            if (text.Length <= max) return text;
            return text.Substring(0, max / 2) + "\n\n...[TRUNCATED]...\n\n" + text.Substring(text.Length - max / 2, max / 2);
        }

        private string SanitizeForPrompt(string s)
        {
            var sb = new StringBuilder();
            foreach (var ch in s)
                if (ch != '\0' && (!char.IsControl(ch) || ch == '\n' || ch == '\r' || ch == '\t')) sb.Append(ch);
            return sb.ToString();
        }
    }
}
