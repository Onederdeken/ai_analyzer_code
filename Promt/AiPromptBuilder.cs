using frontAIagent.Models;
using frontAIagent.Pages;

namespace frontAIagent.Promt
{
    using System.Text;

    public class AiPromptBuilder : IAiPromptBuilder
    {
        // Configuration
        private readonly int _maxCharsPerFile = 4000;
        private readonly int _maxTotalContextChars = 35000;
        private readonly int _maxFilesToInclude = 80;

        public AiPromptBuilder()
        {
        }

        public async Task<string> BuildPromptAsync(
            SavedProject project,
            string userMessage,
            FileReadResult fileContext,
            string projectStructure,
            string personaHint = "Ты — Senior Developer Assistant. Твоя задача: помогать писать рабочий код, давать живые примеры, объяснять решения простым языком.",
            string? logs = null)
        {
            await Task.CompletedTask;

            var sb = new StringBuilder();
            switch (project.FileType)
            {
                case ".py":
                    personaHint = "Ты — Senior Developer Assistant. Твоя задача: помогать писать рабочий код, давать живые примеры, объяснять решения простым языком.";
                    break;
                default:break;
            }

            // ============================================================================
            // 1) SYSTEM / PERSONA — улучшенная версия
            // ============================================================================
            sb.AppendLine("=== SYSTEM / PERSONA ===");
            sb.AppendLine(personaHint.Trim());
            sb.AppendLine();
            sb.AppendLine("Ты работаешь как AI-агент, который анализирует проект, код и логи.");
            sb.AppendLine("Твои правила:");
            sb.AppendLine("— Отвечай строго на русском языке.");
            sb.AppendLine("— Пиши пример кода прямо с файлами и путями..");
            sb.AppendLine("— Не делай сухие списки, пиши живой текст с Markdown и вставками кода.");
            sb.AppendLine("— Всегда учитывай данные файлов, которые тебе прислали..");
            sb.AppendLine("— ВСЁ оформление ответа должно быть удобным для чтения и копирования.");
            sb.AppendLine("— Общайся как коллега-разработчик, который хочет реально помочь.");
            sb.AppendLine("— Если нужно, объясни шаг за шагом, но коротко.");
    
            sb.AppendLine();
            sb.AppendLine("Фильтрация запросов:");
            //sb.AppendLine("Если запрос НЕ относится к проекту/коду/логам, ответь строго одной фразой:");
            //sb.AppendLine("\"Этот запрос не относится к проекту. Я работаю только в контексте текущего кода.\"");
            sb.AppendLine();

            // ============================================================================
            // 2) PROJECT METADATA
            // ============================================================================
            sb.AppendLine("=== PROJECT METADATA ===");
            sb.AppendLine($"Project name: {project?.AnalysisName ?? "(нет имени)"}");
            sb.AppendLine($"Project id: {project?.Id.ToString() ?? "(нет id)"}");
            sb.AppendLine($"Directory path: {project?.DirectoryPath ?? "(неизвестно)"}");
            sb.AppendLine($"File types filter: {project?.FileType ?? "(не указано)"}");
            sb.AppendLine();

            // ============================================================================
            // 3) PROJECT STRUCTURE
            // ============================================================================
            sb.AppendLine("=== PROJECT STRUCTURE ===");
            sb.AppendLine(projectStructure ?? "(нет структуры)");
            sb.AppendLine();

            // ============================================================================
            // 4) Files inclusion policy
            // ============================================================================
            sb.AppendLine("=== FILES & CONTENT (policy) ===");
            sb.AppendLine($"Всего файлов найдено: {fileContext?.TotalFiles ?? 0}, прочитано успешно: {fileContext?.SuccessFiles ?? 0}");
            sb.AppendLine($"Включаю максимум {_maxFilesToInclude} файлов по {_maxCharsPerFile} символов.");
            sb.AppendLine($"Общий лимит контекста: {_maxTotalContextChars} символов.");
            sb.AppendLine();

            // ============================================================================
            // 5) FILES CONTENT
            // ============================================================================
            var totalIncludedChars = 0;
            var includedFiles = 0;

            if (fileContext != null && fileContext.ProcessedFiles.Any())
            {
                sb.AppendLine("=== FILES CONTENT (truncated) ===");

                var prioritized = fileContext.ProcessedFiles
                    .Distinct()
                    .OrderBy(p => GetPriorityForPath(p))
                    .ThenBy(p => p.Length)
                    .ToList();

                foreach (var rel in prioritized)
                {
                    if (includedFiles >= _maxFilesToInclude) break;
                    if (totalIncludedChars >= _maxTotalContextChars) break;

                    var content = ExtractFileContent(fileContext.CombinedContent ?? "", rel);
                    if (content == null) continue;

                    var trimmed = TrimToLength(content, _maxCharsPerFile);
                    trimmed = SanitizeForPrompt(trimmed);

                    sb.AppendLine($"--- FILE: {rel} ---");
                    sb.AppendLine(trimmed);
                    sb.AppendLine();

                    includedFiles++;
                    totalIncludedChars += trimmed.Length;
                }

                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("=== FILES CONTENT ===");
                sb.AppendLine("(Нет доступных файлов для включения.)");
                sb.AppendLine();
            }

            // ============================================================================
            // 6) LOGS
            // ============================================================================
            if (!string.IsNullOrWhiteSpace(logs))
            {
                var logsTrimmed = TrimToLength(logs, Math.Min(8000, _maxTotalContextChars / 2));
                sb.AppendLine("=== LOGS ===");
                sb.AppendLine(SanitizeForPrompt(logsTrimmed));
                sb.AppendLine();
            }

            // ============================================================================
            // 7) USER REQUEST
            // ============================================================================
            sb.AppendLine("=== USER REQUEST ===");
            sb.AppendLine(userMessage.Trim());
            sb.AppendLine();

            // ============================================================================
            // 8) TASK / OUTPUT FORMAT — полностью улучшенная версия
            // ============================================================================
            sb.AppendLine("=== TASK / OUTPUT FORMAT ===");
            sb.AppendLine("Твой ответ должен быть живым, как от коллеги. ");
            sb.AppendLine();
            sb.AppendLine("Если запрос относится к проекту, в ответе обязательно должно быть:");
            sb.AppendLine();
            sb.AppendLine("1) **Краткое резюме** (2–3 предложения).");
            sb.AppendLine();
            sb.AppendLine("2) **Подробный пошаговый план действий**.");
            sb.AppendLine();
            sb.AppendLine("3) **Чёткие указания, какие файлы необходимо изменить**, обязательно в формате:");
            sb.AppendLine();
            sb.AppendLine("   📌 *Файл:* `/path/to/file.ext`");
            sb.AppendLine("   (корректный относительный путь внутри проекта)");
            sb.AppendLine();
            sb.AppendLine("4) Когда нужно добавить код, используй Markdown блоки. ");
            sb.AppendLine();
            sb.AppendLine("   — ```diff");
            sb.AppendLine("     + добавленные строки");
            sb.AppendLine("     - удалённые строки");
            sb.AppendLine("     ```");
            sb.AppendLine();
            sb.AppendLine("   — или блоки вставки кода целиком:");
            sb.AppendLine("     ```language");
            sb.AppendLine("     ...код...");
            sb.AppendLine("     ```");
            sb.AppendLine();
            sb.AppendLine("   — или 'вставить в конец файла':");
            sb.AppendLine("     ```python");
            sb.AppendLine("     # вставить сюда");
            sb.AppendLine("     ```");
            sb.AppendLine();
            sb.AppendLine("Пиши прямо так, чтобы можно было скопировать код и вставить. ");
            sb.AppendLine();
            sb.AppendLine("Не делай сухих списков шагов, только когда это реально помогает понять код.");
            sb.AppendLine();
            sb.AppendLine("Важно: изменения должны быть оформлены ЧЁТКО и ОТДЕЛЬНО, чтобы их можно было легко перенести в проект.");
            sb.AppendLine();
            sb.AppendLine("5) **Инструкции по проверке результата**, включая:");
            sb.AppendLine("   — как запустить проект;");
            sb.AppendLine("   — какие команды выполнить;");
            sb.AppendLine("   — что должно получиться.");
            sb.AppendLine();
            sb.AppendLine("Если информации недостаточно — попроси недостающие файлы.");
            sb.AppendLine();
            sb.AppendLine("Если запрос НЕ относится к проекту/коду — ответь строго:");
            sb.AppendLine("\"Этот запрос не относится к проекту. Я работаю только в контексте текущего кода.\"");
            sb.AppendLine();
       


            // ============================================================================
            // FINAL TRIM
            // ============================================================================
            var result = sb.ToString();

            if (result.Length > _maxTotalContextChars * 2)
            {
                result = result.Substring(0, _maxTotalContextChars * 2) +
                         "\n\n[TRUNCATED CONTEXT DUE TO SIZE LIMIT]";
            }

            return result;
        }

        // Helpers
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
