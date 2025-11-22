using frontAIagent.Models;
using frontAIagent.Pages;

namespace frontAIagent.Promt
{
    public class AiPromptBuilder : IAiPromptBuilder
    {
        public async Task<string> BuildPromptAsync(
            SavedProject project,
            string userMessage,
            FileReadResult fileContext,
            string projectStructure)
        {
            // ❗ Заглушка — здесь будет генерация промта
            await Task.CompletedTask;

            return @$"
[PROMPT BUILDER PLACEHOLDER]

User Query:
{userMessage}

Project: {project.AnalysisName}
Files: {fileContext.TotalFiles}

Structure (placeholder):
{projectStructure}

Content (placeholder):
{fileContext.CombinedContent.Substring(0, Math.Min(500, fileContext.CombinedContent.Length))}...

";
        }
    }

}
