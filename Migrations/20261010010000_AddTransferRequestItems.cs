using LibraryManagementSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibraryManagementSystem.Migrations;

[DbContext(typeof(LibraryDbContext))]
[Migration("20261010010000_AddTransferRequestItems")]
public partial class AddTransferRequestItems : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "TransferRequestItems",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                TransferRequestId = table.Column<int>(type: "int", nullable: false),
                ItemId = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TransferRequestItems", x => x.Id);
                table.ForeignKey(
                    name: "FK_TransferRequestItems_Items_ItemId",
                    column: x => x.ItemId,
                    principalTable: "Items",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_TransferRequestItems_TransferRequests_TransferRequestId",
                    column: x => x.TransferRequestId,
                    principalTable: "TransferRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_TransferRequestItems_ItemId",
            table: "TransferRequestItems",
            column: "ItemId");

        migrationBuilder.CreateIndex(
            name: "IX_TransferRequestItems_TransferRequestId",
            table: "TransferRequestItems",
            column: "TransferRequestId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "TransferRequestItems");
    }
}
