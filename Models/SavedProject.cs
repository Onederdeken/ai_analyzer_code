using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace frontAIagent.Models
{
    [Table("saved_projects")]
    public class SavedProject
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public int Id { get; set; }

        [Required(ErrorMessage = "Analysis name is required")]
        [StringLength(255, ErrorMessage = "Analysis name is too long")]
        [Column("analysis_name")]
        public string AnalysisName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Directory path is required")]
        [Column("directory_path")]
        public string DirectoryPath { get; set; } = string.Empty;

        [Required(ErrorMessage = "File type is required")]
        [StringLength(50)]
        [Column("file_type")]
        public string FileType { get; set; } = ".py";

        [Required(ErrorMessage = "Program description is required")]
        [Column("program_description")]
        public string ProgramDescription { get; set; } = string.Empty;

        [Column("log_path")]
        public string? LogPath { get; set; }

        [Column("last_analyzed")]
        public DateTime LastAnalyzed { get; set; } = DateTime.UtcNow;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}