using frontAIagent.Models;
using frontAIagent.Pages;

namespace frontAIagent.Promt
{
    public interface IAiPromptBuilder
    {
        Task<string> BuildPromptAsync(
            SavedProject project,
            string userMessage,
            FileReadResult fileContext,
            string projectStructure
        );
    }
}
