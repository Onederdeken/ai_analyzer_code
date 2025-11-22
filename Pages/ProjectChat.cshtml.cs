using frontAIagent.Models;
using frontAIagent.Promt;
using frontAIagent.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;

namespace frontAIagent.Pages
{
    public class ProjectChatModel : PageModel
    {
        private readonly IProjectRepository _projectRepository;
        private readonly AiClient _aiClient;
        private readonly IAiPromptBuilder _promptBuilder;

        public ProjectChatModel(
            IProjectRepository projectRepository,
            AiClient aiClient,
            IAiPromptBuilder promptBuilder)
        {
            _projectRepository = projectRepository;
            _aiClient = aiClient;
            _promptBuilder = promptBuilder;
        }

        [BindProperty]
        public SavedProject Project { get; set; } = new SavedProject();

        [BindProperty]
        public string UserMessage { get; set; } = string.Empty;

        public FileReadResult FileContext { get; set; } = new FileReadResult();
        public List<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
        public string AnalysisResult { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string SuccessMessage { get; set; } = string.Empty;
        public string ProjectStructure { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync(int projectId)
        {
            try
            {
                var project = await _projectRepository.GetProjectByIdAsync(projectId);
                if (project == null)
                {
                    ErrorMessage = "Project not found";
                    return RedirectToPage("/Index");
                }

                Project = project;

                // Загружаем и анализируем файлы
                await LoadProjectFilesAsync();
                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading project: {ex.Message}";
                return RedirectToPage("/Index");
            }
        }

        public async Task<IActionResult> OnPostProjectAsync(int projectId)
        {
            try
            {
                var project = await _projectRepository.GetProjectByIdAsync(projectId);
                if (project == null)
                {
                    ErrorMessage = "Project not found";
                    return RedirectToPage("/Index");
                }

                Project = project;
                await LoadProjectFilesAsync();
                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading project: {ex.Message}";
                return RedirectToPage("/Index");
            }
        }

        public async Task<IActionResult> OnPostAskGptAsync(int projectId, string userMessage)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userMessage))
                {
                    ErrorMessage = "Message cannot be empty";
                    return Page();
                }

                UserMessage = userMessage;

                var project = await _projectRepository.GetProjectByIdAsync(projectId);
                Project = project ?? throw new Exception("Project not found");

                // Загружаем файлы проекта
                await LoadProjectFilesAsync();

                // Добавляем в чат пользователя
                ChatMessages.Add(new ChatMessage
                {
                    Content = userMessage,
                    IsUser = true,
                    Timestamp = DateTime.Now
                });

                // ❗ НОВОЕ: создаём общий промт
                var fullPrompt = await _promptBuilder.BuildPromptAsync(
                    Project,
                    userMessage,
                    FileContext,
                    ProjectStructure
                );

                // Отправляем в OpenAI
                var gptResponse = await _aiClient.SendPromptAsync(fullPrompt);

                ChatMessages.Add(new ChatMessage
                {
                    Content = gptResponse,
                    IsUser = false,
                    Timestamp = DateTime.Now
                });

                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                return Page();
            }
        }

        private async Task LoadProjectFilesAsync()
        {
            try
            {
                // Получаем список файлов
                var files = GetFiles(Project.DirectoryPath, Project.FileType);

                // Читаем и объединяем файлы
                FileContext = await ReadAndCombineFilesAsync(files, Project.DirectoryPath);

                // Генерируем структуру проекта
                ProjectStructure = GenerateProjectStructure(files, Project.DirectoryPath);

                // Устанавливаем сообщения
                if (FileContext.IsSuccess)
                {
                    SuccessMessage = $"Successfully loaded {FileContext.SuccessFiles} files from project";
                    if (FileContext.FailedFiles > 0)
                    {
                        SuccessMessage += $", {FileContext.FailedFiles} files failed to read";
                    }
                }
                else
                {
                    ErrorMessage = "Failed to read any files from project";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading project files: {ex.Message}";
            }
        }

        public List<string> GetFiles(string directoryPath, string fileType)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                    return new List<string>();

                var files = new List<string>();

                // Папки для исключения
                var excludedFolders = new[] { "venv", "node_modules", "bin", "obj", "__pycache__", ".git" };

                if (fileType.Contains(","))
                {
                    var types = fileType.Split(',');
                    foreach (var type in types)
                    {
                        var pattern = $"*{type.Trim()}";
                        var foundFiles = Directory.GetFiles(directoryPath, pattern, SearchOption.AllDirectories);

                        // Фильтруем файлы из исключенных папок
                        foreach (var file in foundFiles)
                        {
                            if (!IsInExcludedFolder(file, excludedFolders))
                            {
                                files.Add(file);
                            }
                        }
                    }
                }
                else
                {
                    var pattern = fileType.StartsWith("*") ? fileType : $"*{fileType}";
                    var foundFiles = Directory.GetFiles(directoryPath, pattern, SearchOption.AllDirectories);

                    // Фильтруем файлы из исключенных папок
                    foreach (var file in foundFiles)
                    {
                        if (!IsInExcludedFolder(file, excludedFolders))
                        {
                            files.Add(file);
                        }
                    }
                }

                return files;
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }

        private bool IsInExcludedFolder(string filePath, string[] excludedFolders)
        {
            foreach (var folder in excludedFolders)
            {
                if (filePath.Contains($"/{folder}/") || filePath.Contains($"\\{folder}\\"))
                {
                    return true;
                }
            }
            return false;
        }

        public async Task<FileReadResult> ReadAndCombineFilesAsync(List<string> filePaths, string baseDirectory)
        {
            var result = new FileReadResult();
            var contentBuilder = new StringBuilder();

            foreach (var filePath in filePaths)
            {
                try
                {
                    var relativePath = Path.GetRelativePath(baseDirectory, filePath);
                    var content = await System.IO.File.ReadAllTextAsync(filePath);

                    contentBuilder.AppendLine($"----------------{relativePath}----------------");
                    contentBuilder.AppendLine(content);
                    contentBuilder.AppendLine();

                    result.SuccessFiles++;
                    result.ProcessedFiles.Add(relativePath);
                }
                catch (Exception ex)
                {
                    contentBuilder.AppendLine($"----------------{filePath}----------------");
                    contentBuilder.AppendLine($"[Error reading file: {ex.Message}]");
                    contentBuilder.AppendLine();

                    result.FailedFiles++;
                    result.Errors.Add($"{filePath}: {ex.Message}");
                }
            }

            result.CombinedContent = contentBuilder.ToString();
            result.TotalFiles = filePaths.Count;
            result.IsSuccess = result.SuccessFiles > 0;

            return result;
        }

        private string GenerateProjectStructure(List<string> filePaths, string baseDirectory)
        {
            var structure = new StringBuilder();
            structure.AppendLine("PROJECT STRUCTURE (FILTERED)");
            structure.AppendLine("============================");

            var directories = new Dictionary<string, List<string>>();

            // Группируем файлы по директориям
            foreach (var filePath in filePaths)
            {
                var relativePath = Path.GetRelativePath(baseDirectory, filePath);
                var directory = Path.GetDirectoryName(relativePath) ?? "";
                var fileName = Path.GetFileName(relativePath);

                if (!directories.ContainsKey(directory))
                {
                    directories[directory] = new List<string>();
                }
                directories[directory].Add(fileName);
            }

            // Выводим структуру
            foreach (var (directory, files) in directories.OrderBy(d => d.Key))
            {
                if (string.IsNullOrEmpty(directory))
                {
                    structure.AppendLine("[ROOT]");
                }
                else
                {
                    structure.AppendLine($"[{directory}/]");
                }

                foreach (var file in files.OrderBy(f => f))
                {
                    structure.AppendLine($"   {file}");
                }
                structure.AppendLine();
            }

            structure.AppendLine($"Summary: {filePaths.Count} files in {directories.Count} directories");
            structure.AppendLine("Excluded: venv/, node_modules/, bin/, obj/, __pycache__/ folders");

            return structure.ToString();
        }

        public async Task<IActionResult> OnPostSendMessageAsync(int projectId, string userMessage)
        {
            try
            {
                var project = await _projectRepository.GetProjectByIdAsync(projectId);
                if (project == null)
                {
                    ErrorMessage = "Project not found";
                    return RedirectToPage("/Index");
                }

                Project = project;
                UserMessage = userMessage;

                if (string.IsNullOrEmpty(userMessage))
                {
                    ErrorMessage = "Message cannot be empty";
                    return Page();
                }

                // Загружаем файлы при каждом сообщении
                await LoadProjectFilesAsync();

                ChatMessages.Add(new ChatMessage
                {
                    Content = userMessage,
                    IsUser = true,
                    Timestamp = DateTime.Now
                });

                project.LastAnalyzed = DateTime.UtcNow;
                await _projectRepository.UpdateProjectAsync(project);

                AnalysisResult = await AnalyzeWithAI(project, userMessage);

                ChatMessages.Add(new ChatMessage
                {
                    Content = AnalysisResult,
                    IsUser = false,
                    Timestamp = DateTime.Now
                });

                UserMessage = string.Empty;
                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error sending message: {ex.Message}";
                return Page();
            }
        }

        private async Task<string> AnalyzeWithAI(SavedProject project, string userMessage)
        {
            await Task.Delay(1000);

            return $@"AI ANALYSIS RESPONSE

Your Question: {userMessage}

Project: {project.AnalysisName}
Files: {FileContext.TotalFiles} total ({FileContext.SuccessFiles} readable)
Structure: {ProjectStructure.Split('\n').Count(l => l.Contains("["))} directories

Analysis:

Based on the project structure and {FileContext.SuccessFiles} readable files, here are my findings:

Project Organization: {GetOrganizationAssessment()}
Code Quality: Needs detailed analysis of actual code
Suggestions: Review the specific file contents for detailed recommendations

Would you like me to analyze specific files or aspects in more detail?";
        }

        private string GetOrganizationAssessment()
        {
            var dirCount = ProjectStructure.Split('\n').Count(l => l.Contains("[") && l.Contains("]"));
            if (dirCount > 5) return "Well-structured with multiple directories";
            if (dirCount > 2) return "Moderately organized";
            return "Simple flat structure - consider organizing into folders";
        }
    }

    public class FileReadResult
    {
        public bool IsSuccess { get; set; }
        public string CombinedContent { get; set; } = string.Empty;
        public int TotalFiles { get; set; }
        public int SuccessFiles { get; set; }
        public int FailedFiles { get; set; }
        public List<string> ProcessedFiles { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();

        public string GetSummary()
        {
            return $"Processed {TotalFiles} files: {SuccessFiles} successful, {FailedFiles} failed";
        }
    }

    public class ChatMessage
    {
        public string Content { get; set; } = string.Empty;
        public bool IsUser { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}