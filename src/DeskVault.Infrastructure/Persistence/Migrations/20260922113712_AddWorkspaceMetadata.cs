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
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");
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
