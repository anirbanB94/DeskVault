using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeskVault.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersistDocumentChunkSourceLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceLocationEndLine",
                table: "DocumentChunks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceLocationStartLine",
                table: "DocumentChunks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_DocumentChunks_SourceLocationRange",
                table: "DocumentChunks",
                sql: "(\"SourceLocationStartLine\" IS NULL\r\n    AND \"SourceLocationEndLine\" IS NULL)\r\nOR\r\n(\"SourceLocationStartLine\" IS NOT NULL\r\n    AND \"SourceLocationEndLine\" IS NOT NULL\r\n    AND \"SourceLocationStartLine\" > 0\r\n    AND \"SourceLocationEndLine\" >= \"SourceLocationStartLine\")");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_DocumentChunks_SourceLocationRange",
                table: "DocumentChunks");

            migrationBuilder.DropColumn(
                name: "SourceLocationEndLine",
                table: "DocumentChunks");

            migrationBuilder.DropColumn(
                name: "SourceLocationStartLine",
                table: "DocumentChunks");
        }
    }
}
