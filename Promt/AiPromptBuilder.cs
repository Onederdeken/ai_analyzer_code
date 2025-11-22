using frontAIagent.Models;
using frontAIagent.Pages;

namespace frontAIagent.Promt
{
    // AiPromptBuilder.cs
    using System.Text;

    public class AiPromptBuilder : IAiPromptBuilder
    {
        // Configuration - tune limits as needed
        private readonly int _maxCharsPerFile = 4000;      // max chars to include from one file
        private readonly int _maxTotalContextChars = 35000; // overall cap for files+logs
        private readonly int _maxFilesToInclude = 80;      // cap on number of files included

        public AiPromptBuilder()
        {
        }

        public async Task<string> BuildPromptAsync(
            SavedProject project,
            string userMessage,
            FileReadResult fileContext,
            string projectStructure,
            string personaHint = "Представь, что ты senior Python developer с опытом 10+ лет",
            string? logs = null)
        {
            // no blocking calls but keep signature async-friendly
            await Task.CompletedTask;

            var sb = new StringBuilder();

            // 1) System / persona header
            sb.AppendLine("=== SYSTEM / PERSONA ===");
            sb.AppendLine(personaHint.Trim());
            sb.AppendLine();
            sb.AppendLine("Инструкции по тону и языку ответов:");
            sb.AppendLine("- Отвечай строго на русском языке.");
            sb.AppendLine("- Действуй как практичный инженер: давай точные, воспроизводимые шаги.");
            sb.AppendLine("- В ответах обязательно указывай файлы и точные правки (файл, где править, какие строки/фрагменты заменить/добавить).");
            sb.AppendLine("- Если запрос НЕ относится к текущему проекту/коду, ответь коротко и строго: \"Это вне контекста текущего проекта. Не могу помочь.\"");
            sb.AppendLine();

            // 2) Project metadata
            sb.AppendLine("=== PROJECT METADATA ===");
            sb.AppendLine($"Project name: {project?.AnalysisName ?? "(нет имени)"}");
            sb.AppendLine($"Project id: {project?.Id.ToString() ?? "(нет id)"}");
            sb.AppendLine($"Directory path: {project?.DirectoryPath ?? "(неизвестно)"}");
            sb.AppendLine($"File types filter: {project?.FileType ?? "(не указано)"}");
            sb.AppendLine();

            // 3) Project structure
            sb.AppendLine("=== PROJECT STRUCTURE (кратко) ===");
            sb.AppendLine(projectStructure ?? "(нет структуры)");
            sb.AppendLine();

            // 4) Files summary & inclusion policy
            sb.AppendLine("=== FILES & CONTENT (policy) ===");
            sb.AppendLine($"Всего файлов найдено: {fileContext?.TotalFiles ?? 0}, успешно прочитано: {fileContext?.SuccessFiles ?? 0}.");
            sb.AppendLine($"Включаю максимум {_maxFilesToInclude} файлов, по {_maxCharsPerFile} символов каждого, и общий предел {_maxTotalContextChars} символов для контекста.");
            sb.AppendLine();

            // 5) Files content (trimmed & structured). Try to include most important files first:
            var totalIncludedChars = 0;
            var includedFiles = 0;

            if (fileContext != null && fileContext.ProcessedFiles.Any())
            {
                sb.AppendLine("=== FILES CONTENT (truncated) ===");

                // If we have full paths? fileContext.ProcessedFiles contains relative paths already in earlier code.
                // We'll attempt to include files in this order: critical extensions first (.cs, .py, .json, .config, .xml, .txt)
                var prioritized = fileContext.ProcessedFiles
                    .Distinct()
                    .Select(p => p)
                    .OrderBy(p => GetPriorityForPath(p))
                    .ThenBy(p => p.Length) // shorter names first
                    .ToList();

                foreach (var rel in prioritized)
                {
                    if (includedFiles >= _maxFilesToInclude) break;
                    if (totalIncludedChars >= _maxTotalContextChars) break;

                    // find original full path content in fileContext.CombinedContent if available,
                    // but fileContext only has CombinedContent. To keep this generic, try to extract by delimiter
                    // we assume CombinedContent was built with "----------------{relativePath}----------------"
                    var fileContent = ExtractFileContent(fileContext.CombinedContent ?? string.Empty, rel);

                    if (fileContent == null)
                        continue;

                    // sanitize and trim
                    var trimmed = TrimToLength(fileContent, _maxCharsPerFile);
                    var safeTrimmed = SanitizeForPrompt(trimmed);

                    sb.AppendLine($"--- FILE: {rel} ---");
                    sb.AppendLine(safeTrimmed);
                    sb.AppendLine(); // spacer

                    includedFiles++;
                    totalIncludedChars += safeTrimmed.Length;
                }

                if (includedFiles == 0)
                {
                    sb.AppendLine("(Не удалось извлечь содержимое файлов из CombinedContent. Если CombinedContent отсутствует, передайте полный текст файлов отдельно.)");
                }
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("(Нет прочитанных файлов для включения.)");
                sb.AppendLine();
            }

            // 6) Logs (if provided)
            if (!string.IsNullOrWhiteSpace(logs))
            {
                var logsTrimmed = TrimToLength(logs, Math.Min(8000, _maxTotalContextChars / 2));
                sb.AppendLine("=== RELEVANT LOGS ===");
                sb.AppendLine(SanitizeForPrompt(logsTrimmed));
                sb.AppendLine();
            }

            // 7) User request & what is expected
            sb.AppendLine("=== USER REQUEST ===");
            sb.AppendLine(userMessage.Trim());
            sb.AppendLine();

            sb.AppendLine("=== TASK / OUTPUT FORMAT ===");
            sb.AppendLine("1) Если запрос относится к проекту/коду: дай детальную инструкцию как внести изменения. Обязательно укажи:");
            sb.AppendLine("   - какие файлы изменить (относительный путь),");
            sb.AppendLine("   - точные изменения (показать diff или вставку кода),");
            sb.AppendLine("   - шаги для тестирования изменений и команду для запуска,");
            sb.AppendLine("   - если нужно обновить конфигурацию/зависимости — укажи точные команды.");
            sb.AppendLine();
            sb.AppendLine("2) Если запрос НЕ относится к проекту/коду (например общие вопросы, философия, просьбы написать эссе), ответь коротко: \"Это вне контекста текущего проекта. Не могу помочь.\"");
            sb.AppendLine();
            sb.AppendLine("3) Язык: русский (все ответы ТОЛЬКО на русском).");
            sb.AppendLine();
            sb.AppendLine("4) Формат ответа: сначала краткий вывод (2-3 предложения), затем подробный пошаговый план с кодом/патчами.");
            sb.AppendLine();

            // 8) Safety / size hint
            sb.AppendLine("=== NOTE ===");
            sb.AppendLine("Если необходимая информация отсутствует (файлы/логи не включены), предупреди кратко и попроси прислать нужные файлы/логи.");
            sb.AppendLine();

            // Final
            var prompt = sb.ToString();

            // Final overall trimming to _maxTotalContextChars * 1.5 as safety (we included text also in header)
            if (prompt.Length > _maxTotalContextChars * 2)
            {
                prompt = prompt.Substring(0, _maxTotalContextChars * 2);
                prompt += "\n\n[TRUNCATED CONTEXT DUE TO SIZE LIMITS]";
            }

            return prompt;
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
            if (string.IsNullOrEmpty(combined) || string.IsNullOrEmpty(relativePath)) return null;

            // combined is expected to contain separators like ----------------{relativePath}----------------
            var marker = $"----------------{relativePath}----------------";
            var idx = combined.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0) return null;

            idx += marker.Length;
            // find next marker (start of next file) or end of string
            var nextIdx = combined.IndexOf("----------------", idx, StringComparison.Ordinal);
            if (nextIdx < 0) nextIdx = combined.Length;

            var content = combined.Substring(idx, nextIdx - idx).Trim();
            return content;
        }

        private string TrimToLength(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            if (text.Length <= max) return text;
            // try to keep head+tail
            var head = text.Substring(0, max / 2);
            var tail = text.Substring(text.Length - max / 2, max / 2);
            return head + "\n\n...[TRUNCATED]...\n\n" + tail;
        }

        private string SanitizeForPrompt(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            // remove nulls and control chars that may confuse model or transport
            var sb = new StringBuilder(s.Length);
            foreach (var ch in s)
            {
                if (ch == '\0') continue;
                // keep common whitespace and printable characters
                if (char.IsControl(ch) && ch != '\r' && ch != '\n' && ch != '\t') continue;
                sb.Append(ch);
            }
            return sb.ToString();
        }
    }


}
