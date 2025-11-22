using frontAIagent.Models;
using frontAIagent.Pages;
using System.Text;

namespace frontAIagent.Promt
{
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
            string personaHint = "Ты — Senior Developer Assistant. Помогай писать рабочий код, объясняй простым языком и давай примеры.",
            string? logs = null)
        {
            await Task.CompletedTask;

            var sb = new StringBuilder();

            // 1) SYSTEM / PERSONA
            sb.AppendLine("=== SYSTEM / PERSONA ===");
            sb.AppendLine(personaHint);
            sb.AppendLine();
            sb.AppendLine("Ты — AI-ассистент, анализируешь проект, код и логи.");
            sb.AppendLine("Правила:");
            sb.AppendLine("— Отвечай на русском, живо и понятно.");
            sb.AppendLine("— Код всегда в Markdown-блоках, пояснения отдельным текстом.");
            sb.AppendLine("— Используй пустые строки между заголовками, блоками кода и текстом.");
            sb.AppendLine("— Общайся как коллега-разработчик, кратко и понятно.");
            sb.AppendLine();

            // 2) PROJECT METADATA
            sb.AppendLine("=== PROJECT METADATA ===");
            sb.AppendLine($"Project name: {project?.AnalysisName ?? "(нет имени)"}");
            sb.AppendLine($"Project id: {project?.Id.ToString() ?? "(нет id)"}");
            sb.AppendLine($"Directory path: {project?.DirectoryPath ?? "(неизвестно)"}");
            sb.AppendLine($"File types filter: {project?.FileType ?? "(не указано)"}");
            sb.AppendLine();

            // 3) PROJECT STRUCTURE
            sb.AppendLine("=== PROJECT STRUCTURE ===");
            sb.AppendLine(projectStructure ?? "(нет структуры)");
            sb.AppendLine();

            // 4) FILES CONTENT
            sb.AppendLine("=== FILES CONTENT (truncated) ===");
            if (fileContext?.ProcessedFiles != null && fileContext.ProcessedFiles.Any())
            {
                var totalChars = 0;
                var includedFiles = 0;

                foreach (var rel in fileContext.ProcessedFiles
                             .Distinct()
                             .OrderBy(GetPriorityForPath)
                             .ThenBy(f => f.Length))
                {
                    if (includedFiles >= _maxFilesToInclude || totalChars >= _maxTotalContextChars) break;

                    var content = ExtractFileContent(fileContext.CombinedContent ?? "", rel);
                    if (string.IsNullOrEmpty(content)) continue;

                    var trimmed = TrimToLength(content, _maxCharsPerFile);
                    trimmed = SanitizeForPrompt(trimmed);

                    // Код в блоке Markdown
                    sb.AppendLine($"📌 *Файл:* {rel}");
                    sb.AppendLine("```csharp");
                    sb.AppendLine(trimmed);
                    sb.AppendLine("```");
                    sb.AppendLine();

                    includedFiles++;
                    totalChars += trimmed.Length;
                }
            }
            else
            {
                sb.AppendLine("(Нет доступных файлов для включения.)");
            }

            sb.AppendLine();

            // 5) LOGS
            if (!string.IsNullOrWhiteSpace(logs))
            {
                var logsTrimmed = TrimToLength(logs, Math.Min(8000, _maxTotalContextChars / 2));
                sb.AppendLine("=== LOGS ===");
                sb.AppendLine("```text");
                sb.AppendLine(SanitizeForPrompt(logsTrimmed));
                sb.AppendLine("```");
                sb.AppendLine("Изучи логи и если есть какие узкие места проблемы текущие или возможные будущие проблемы сообщи");
                sb.AppendLine();
            }

            // 6) USER REQUEST
            sb.AppendLine("=== USER REQUEST ===");
            sb.AppendLine(userMessage.Trim());
            sb.AppendLine();

            // 7) TASK / OUTPUT FORMAT
            sb.AppendLine("=== TASK / OUTPUT FORMAT ===");
            sb.AppendLine("Ответ должен быть живым, как от коллеги-разработчика:");
            sb.AppendLine("- Код в Markdown-блоках, пояснения отдельным текстом.");
            sb.AppendLine("- Между блоками пустые строки для читаемости.");
            sb.AppendLine("- Diff или вставки кода оформляй так:");
            sb.AppendLine("```diff\n+ добавлено\n- удалено\n```");
            sb.AppendLine("Или целиком:\n```csharp\n...код...\n```");
            sb.AppendLine("Или 'вставить в конец файла':\n```python\n# вставить сюда\n```");
            sb.AppendLine("- Пиши так, чтобы можно было сразу копировать и вставлять код.");
            sb.AppendLine("- Объяснения короткие, по сути, не сухие списки.");
            sb.AppendLine();

            sb.AppendLine("Инструкции по проверке:");
            sb.AppendLine("— как запустить проект;");
            sb.AppendLine("— команды для проверки;");
            sb.AppendLine("— ожидаемый результат.");
            sb.AppendLine();

            sb.AppendLine("Если информации недостаточно — попроси недостающие файлы.");
            sb.AppendLine("Если запрос не относится к проекту/коду — ответь строго:");
            sb.AppendLine("\"Этот запрос не относится к проекту. Я работаю только в контексте текущего кода.\"");
            sb.AppendLine();

            // Ограничение на размер
            var result = sb.ToString();
            if (result.Length > _maxTotalContextChars * 2)
                result = result.Substring(0, _maxTotalContextChars * 2) + "\n\n[TRUNCATED CONTEXT DUE TO SIZE LIMIT]";

            return result;
        }

        // --- Helpers ---
        private static int GetPriorityForPath(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".cs" => 1,
                ".py" => 1,
                ".js" => 2,
                ".ts" => 2,
                ".json" => 2,
                ".xml" => 3,
                ".config" => 3,
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
            return text.Substring(0, max / 2) +
                   "\n\n...[TRUNCATED]...\n\n" +
                   text.Substring(text.Length - max / 2, max / 2);
        }

        private string SanitizeForPrompt(string s)
        {
            var sb = new StringBuilder();
            foreach (var ch in s)
            {
                if (ch == '\0') continue;
                if (char.IsControl(ch) && ch != '\n' && ch != '\r' && ch != '\t') continue;
                sb.Append(ch);
            }
            return sb.ToString();
        }
    }
}
