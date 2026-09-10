using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeskVault.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLastSuccessfulProcessingGeneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "LastSuccessfulProcessingGeneration",
                table: "Documents",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastSuccessfulProcessingGeneration",
                table: "Documents");
        }
    }
}
