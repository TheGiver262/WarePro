using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using QuanLyHangHoa.Data;

#nullable disable

namespace QuanLyHangHoa.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260713090100_AddStockReversalSourceConstraint")]
public sealed class AddStockReversalSourceConstraint : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "UX_StockAdjustment_Reversal_Source",
            table: "StockAdjustment",
            columns: new[]
            {
                "ReferenceDocumentType",
                "ReferenceDocumentId",
                "AdjustmentType"
            },
            unique: true,
            filter: "[AdjustmentType] = 'Reversal' AND [ReferenceDocumentType] IS NOT NULL AND [ReferenceDocumentId] IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "UX_StockAdjustment_Reversal_Source",
            table: "StockAdjustment");
    }
}
