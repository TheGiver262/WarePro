using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using QuanLyHangHoa.Data;

#nullable disable

namespace QuanLyHangHoa.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260713090200_LinkStockCountCorrections")]
public sealed class LinkStockCountCorrections : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "DraftSerials",
            table: "StockAdjustmentLine",
            type: "nvarchar(4000)",
            maxLength: 4000,
            nullable: true);

        AddCorrectionColumns(migrationBuilder, "StockIn");
        AddCorrectionColumns(migrationBuilder, "StockOut");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        DropCorrectionColumns(migrationBuilder, "StockOut");
        DropCorrectionColumns(migrationBuilder, "StockIn");
        migrationBuilder.DropColumn(name: "DraftSerials", table: "StockAdjustmentLine");
    }

    private static void AddCorrectionColumns(MigrationBuilder migrationBuilder, string table)
    {
        migrationBuilder.AddColumn<int>(name: "StockCountLineId", table: table, type: "int", nullable: true);
        migrationBuilder.AddColumn<int>(name: "StockCountSessionId", table: table, type: "int", nullable: true);
        migrationBuilder.CreateIndex(
            name: $"IX_{table}_StockCountSessionId",
            table: table,
            column: "StockCountSessionId");
        migrationBuilder.CreateIndex(
            name: $"UX_{table}_StockCountLineId",
            table: table,
            column: "StockCountLineId",
            unique: true,
            filter: "[StockCountLineId] IS NOT NULL");
        migrationBuilder.AddForeignKey(
            name: $"FK_{table}_StockCountLine",
            table: table,
            column: "StockCountLineId",
            principalTable: "StockCountLine",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: $"FK_{table}_StockCountSession",
            table: table,
            column: "StockCountSessionId",
            principalTable: "StockCountSession",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    private static void DropCorrectionColumns(MigrationBuilder migrationBuilder, string table)
    {
        migrationBuilder.DropForeignKey(name: $"FK_{table}_StockCountLine", table: table);
        migrationBuilder.DropForeignKey(name: $"FK_{table}_StockCountSession", table: table);
        migrationBuilder.DropIndex(name: $"IX_{table}_StockCountSessionId", table: table);
        migrationBuilder.DropIndex(name: $"UX_{table}_StockCountLineId", table: table);
        migrationBuilder.DropColumn(name: "StockCountLineId", table: table);
        migrationBuilder.DropColumn(name: "StockCountSessionId", table: table);
    }
}
