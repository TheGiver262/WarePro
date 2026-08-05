using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyHangHoa.Migrations
{
    /// <inheritdoc />
    public partial class MakeLastStockInLineIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductSerial_LastStockInLine",
                table: "ProductSerial");

            migrationBuilder.AlterColumn<int>(
                name: "LastStockInLineId",
                table: "ProductSerial",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: false);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductSerial_LastStockInLine",
                table: "ProductSerial",
                column: "LastStockInLineId",
                principalTable: "StockInLine",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new InvalidOperationException("Cannot revert nullable LastStockInLineId to NOT NULL without data loss for serials created via non-StockIn operations.");
        }
    }
}
