using System;
using LibraryManagementSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibraryManagementSystem.Migrations;

/// <summary>
/// Adds API usage tracking: last-used time and request count on each API key, and an
/// ApiRequestLogs table with one row per REST API call.
/// </summary>
[DbContext(typeof(LibraryDbContext))]
[Migration("20261013120000_AddApiUsageTracking")]
public partial class AddApiUsageTracking : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "LastUsedAt",
            table: "ApiKeys",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "RequestCount",
            table: "ApiKeys",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.CreateTable(
            name: "ApiRequestLogs",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                Channel = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                ApiKeyId = table.Column<int>(type: "int", nullable: true),
                Path = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                SearchTerm = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                StatusCode = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_ApiRequestLogs", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_ApiRequestLogs_RequestedAt",
            table: "ApiRequestLogs",
            column: "RequestedAt");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ApiRequestLogs");
        migrationBuilder.DropColumn(name: "LastUsedAt", table: "ApiKeys");
        migrationBuilder.DropColumn(name: "RequestCount", table: "ApiKeys");
    }
}
