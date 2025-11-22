using frontAIagent.Models;
using frontAIagent.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace frontAIagent.Pages
{
    public class IndexModel : PageModel
    {
        public List<SavedProject> SavedProjects { get; set; } = new List<SavedProject>();
        private int id { get; set; } = 0;

        [BindProperty]
        public SavedProject SavedProject { get; set; } = new SavedProject();

        public string AnalysisResult { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string SuccessMessage { get; set; } = string.Empty;
        private readonly IProjectRepository _projectRepository;

        public IndexModel(IProjectRepository projectRepository)
        {
            _projectRepository = projectRepository;
        }

        public async Task OnGet()
        {

            await LoadProjectsAsync();
            if (SavedProjects.Count != 0) id = SavedProjects.Last().Id+1;
            else id = 1;
        }
        public async Task SaveProject(SavedProject savedProject)
        {
            var project = await _projectRepository.CreateProjectAsync(savedProject);
            SavedProjects.Insert(0, project);
        }
        private async Task LoadProjectsAsync()
        {
            SavedProjects = (await _projectRepository.GetAllProjectsAsync()).ToList();
        }

        public async Task<IActionResult> OnPostCreateProjectAsync()
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    ErrorMessage = "Please fix the validation errors: " + string.Join(", ", errors);
                    return Page();
                }
                
                await SaveProject(SavedProject);
                SavedProject = new SavedProject();


                return RedirectToPage();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error: {ex.Message}";
               
                return Page();
            }
        }
        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            try
            {
                var success = await _projectRepository.DeleteProjectAsync(id);
                if (success)
                {
                    SuccessMessage = "Project deleted successfully!";
                }
                else
                {
                    ErrorMessage = "Project not found.";
                }

                await LoadProjectsAsync();
                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error deleting project: {ex.Message}";
                await LoadProjectsAsync();
                return Page();
            }
        }


    }


    
}