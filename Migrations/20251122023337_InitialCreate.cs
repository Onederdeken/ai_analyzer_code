using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace frontAIagent.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "saved_projects",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    analysis_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    directory_path = table.Column<string>(type: "text", nullable: false),
                    file_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: ".py"),
                    program_description = table.Column<string>(type: "text", nullable: false),
                    log_path = table.Column<string>(type: "text", nullable: true),
                    last_analyzed = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_projects", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_saved_projects_analysis_name",
                table: "saved_projects",
                column: "analysis_name");

            migrationBuilder.CreateIndex(
                name: "idx_saved_projects_created_at",
                table: "saved_projects",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idx_saved_projects_last_analyzed",
                table: "saved_projects",
                column: "last_analyzed");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "saved_projects");
        }
    }
}
