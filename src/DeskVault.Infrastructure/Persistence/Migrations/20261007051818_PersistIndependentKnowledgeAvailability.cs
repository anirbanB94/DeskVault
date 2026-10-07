using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeskVault.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersistIndependentKnowledgeAvailability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LifecycleState",
                table: "Documents",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProcessingState",
                table: "Documents",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "DocumentKnowledgeAvailabilities",
                columns: table => new
                {
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Representation = table.Column<int>(type: "INTEGER", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    LastAvailableProcessingGeneration = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentKnowledgeAvailabilities", x => new { x.DocumentId, x.Representation });
                    table.CheckConstraint("CK_DocumentKnowledgeAvailabilities_LastAvailableProcessingGeneration_NonNegative", "\"LastAvailableProcessingGeneration\" IS NULL\r\nOR\r\n\"LastAvailableProcessingGeneration\" >= 0");
                    table.ForeignKey(
                        name: "FK_DocumentKnowledgeAvailabilities_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentKnowledgeAvailabilities");

            migrationBuilder.DropColumn(
                name: "LifecycleState",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ProcessingState",
                table: "Documents");
        }
    }
}
