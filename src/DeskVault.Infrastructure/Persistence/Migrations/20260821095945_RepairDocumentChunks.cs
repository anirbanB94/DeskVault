using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeskVault.Infrastructure.Persistence.Migrations;

public partial class RepairDocumentChunks : Migration
{
    protected override void Up(
        MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DocumentChunks",
            columns: table => new
            {
                Id = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),

                DocumentId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),

                Order = table.Column<int>(
                    type: "INTEGER",
                    nullable: false),

                Text = table.Column<string>(
                    type: "TEXT",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_DocumentChunks",
                    x => x.Id);

                table.ForeignKey(
                    name: "FK_DocumentChunks_Documents_DocumentId",
                    column: x => x.DocumentId,
                    principalTable: "Documents",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_DocumentChunks_DocumentId",
            table: "DocumentChunks",
            column: "DocumentId");

        migrationBuilder.CreateIndex(
            name: "IX_DocumentChunks_DocumentId_Order",
            table: "DocumentChunks",
            columns: new[]
            {
                "DocumentId",
                "Order"
            },
            unique: true);
    }

    protected override void Down(
        MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "DocumentChunks");
    }
}
