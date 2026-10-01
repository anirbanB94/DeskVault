using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeskVault.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Workspaces",
                type: "TEXT",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastUpdated",
                table: "Workspaces",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Workspaces"
                SET "LastUpdated" = CURRENT_TIMESTAMP
                WHERE "LastUpdated" IS NULL;
                """);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "LastUpdated",
                table: "Workspaces",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "Workspaces");

            migrationBuilder.DropColumn(
                name: "LastUpdated",
                table: "Workspaces");
        }
    }
}
