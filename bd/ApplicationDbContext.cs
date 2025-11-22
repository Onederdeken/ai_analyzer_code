using Microsoft.EntityFrameworkCore;
using frontAIagent.Models;

namespace frontAIagent.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<SavedProject> SavedProjects { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Конфигурация для SavedProject
            modelBuilder.Entity<SavedProject>(entity =>
            {
                entity.ToTable("saved_projects");

                // Индексы
                entity.HasIndex(e => e.AnalysisName)
                    .HasDatabaseName("idx_saved_projects_analysis_name");

                entity.HasIndex(e => e.LastAnalyzed)
                    .HasDatabaseName("idx_saved_projects_last_analyzed");

                entity.HasIndex(e => e.CreatedAt)
                    .HasDatabaseName("idx_saved_projects_created_at");

                // Значения по умолчанию
                entity.Property(e => e.FileType)
                    .HasDefaultValue(".py");

                entity.Property(e => e.LastAnalyzed)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.UpdatedAt)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
            });
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // Автоматическое обновление UpdatedAt
            var entries = ChangeTracker
                .Entries()
                .Where(e => e.Entity is SavedProject &&
                           (e.State == EntityState.Modified || e.State == EntityState.Added));

            foreach (var entityEntry in entries)
            {
                if (entityEntry.State == EntityState.Modified)
                {
                    ((SavedProject)entityEntry.Entity).UpdatedAt = DateTime.UtcNow;
                }

                if (entityEntry.State == EntityState.Added)
                {
                    ((SavedProject)entityEntry.Entity).CreatedAt = DateTime.UtcNow;
                    ((SavedProject)entityEntry.Entity).UpdatedAt = DateTime.UtcNow;
                }
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}