using frontAIagent.Models;
using frontAIagent.Pages;

namespace frontAIagent.Promt
{
    // IAiPromptBuilder.cs
    public interface IAiPromptBuilder
    {
        /// <summary>
        /// Build a composed prompt that will be sent to the model.
        /// - project: metadata about project (SavedProject)
        /// - userMessage: user's plain request
        /// - fileContext: combined files content and per-file list
        /// - projectStructure: pretty project structure text
        /// - personaHint: short description of the persona/role (optional)
        /// - logs: optional textual logs to include
        /// Returns full prompt text (in Russian) ready to be sent to AI.
        /// </summary>
        Task<string> BuildPromptAsync(
            SavedProject project,
            string userMessage,
            FileReadResult fileContext,
            string projectStructure,
            string personaHint = "Представь, что ты senior Python developer с опытом 10+ лет",
            string? logs = null
        );
    }

}
