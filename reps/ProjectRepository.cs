using Microsoft.EntityFrameworkCore;
using frontAIagent.Data;
using frontAIagent.Models;

namespace frontAIagent.Repositories
{
    public class ProjectRepository : IProjectRepository
    {
        private readonly ApplicationDbContext _context;

        public ProjectRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<SavedProject>> GetAllProjectsAsync()
        {
            return await _context.SavedProjects
                .OrderByDescending(p => p.LastAnalyzed)
                .ToListAsync();
        }

        public async Task<SavedProject?> GetProjectByIdAsync(int id)
        {
            return await _context.SavedProjects
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<SavedProject?> GetProjectByNameAsync(string name)
        {
            return await _context.SavedProjects
                .FirstOrDefaultAsync(p => p.AnalysisName == name);
        }

        public async Task<SavedProject> CreateProjectAsync(SavedProject project)
        {
            project.LastAnalyzed = DateTime.UtcNow;
            project.CreatedAt = DateTime.UtcNow;
            project.UpdatedAt = DateTime.UtcNow;

            _context.SavedProjects.Add(project);
            await _context.SaveChangesAsync();
            return project;
        }

        public async Task<SavedProject> UpdateProjectAsync(SavedProject project)
        {
            project.UpdatedAt = DateTime.UtcNow;
            project.LastAnalyzed = DateTime.UtcNow;

            _context.SavedProjects.Update(project);
            await _context.SaveChangesAsync();
            return project;
        }

        public async Task<bool> DeleteProjectAsync(int id)
        {
            var project = await GetProjectByIdAsync(id);
            if (project == null)
                return false;

            _context.SavedProjects.Remove(project);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<SavedProject>> GetRecentProjectsAsync(int count = 10)
        {
            return await _context.SavedProjects
                .OrderByDescending(p => p.LastAnalyzed)
                .Take(count)
                .ToListAsync();
        }
    }
}