using frontAIagent.Models;

namespace frontAIagent.Repositories
{
    public interface IProjectRepository
    {
        Task<IEnumerable<SavedProject>> GetAllProjectsAsync();
        Task<SavedProject?> GetProjectByIdAsync(int id);
        Task<SavedProject> CreateProjectAsync(SavedProject project);
        Task<SavedProject> UpdateProjectAsync(SavedProject project);
        Task<bool> DeleteProjectAsync(int id);
        Task<SavedProject?> GetProjectByNameAsync(string name);
        Task<IEnumerable<SavedProject>> GetRecentProjectsAsync(int count = 10);
    }
}