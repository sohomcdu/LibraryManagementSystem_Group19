using System;
using LibraryManagementSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibraryManagementSystem.Migrations;

[DbContext(typeof(LibraryDbContext))]
[Migration("20261010000000_AddManagerApprovedTransfers")]
public partial class AddManagerApprovedTransfers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "TransferRequests",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                ItemCategory = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                FromBranchId = table.Column<int>(type: "int", nullable: false),
                ToBranchId = table.Column<int>(type: "int", nullable: false),
                Quantity = table.Column<int>(type: "int", nullable: false),
                RequestedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                ReviewedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DecisionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TransferRequests", x => x.Id);
                table.ForeignKey(
                    name: "FK_TransferRequests_Branches_FromBranchId",
                    column: x => x.FromBranchId,
                    principalTable: "Branches",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_TransferRequests_Branches_ToBranchId",
                    column: x => x.ToBranchId,
                    principalTable: "Branches",
                    principalColumn: "Id");
            });

        migrationBuilder.AddColumn<int>(
            name: "TransferRequestId",
            table: "ItemTransfers",
            type: "int",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_ItemTransfers_TransferRequestId",
            table: "ItemTransfers",
            column: "TransferRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_TransferRequests_FromBranchId",
            table: "TransferRequests",
            column: "FromBranchId");

        migrationBuilder.CreateIndex(
            name: "IX_TransferRequests_ToBranchId",
            table: "TransferRequests",
            column: "ToBranchId");

        migrationBuilder.AddForeignKey(
            name: "FK_ItemTransfers_TransferRequests_TransferRequestId",
            table: "ItemTransfers",
            column: "TransferRequestId",
            principalTable: "TransferRequests",
            principalColumn: "Id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_ItemTransfers_TransferRequests_TransferRequestId",
            table: "ItemTransfers");

        migrationBuilder.DropTable(name: "TransferRequests");

        migrationBuilder.DropIndex(
            name: "IX_ItemTransfers_TransferRequestId",
            table: "ItemTransfers");

        migrationBuilder.DropColumn(
            name: "TransferRequestId",
            table: "ItemTransfers");
    }
}
