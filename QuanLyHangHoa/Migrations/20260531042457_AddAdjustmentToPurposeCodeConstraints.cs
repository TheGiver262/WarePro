using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyHangHoa.Migrations
{
    /// <inheritdoc />
    public partial class AddAdjustmentToPurposeCodeConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop constraint if exists and add it with the new definition
            migrationBuilder.Sql(@"
IF EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_StockOut_PurposeCode')
BEGIN
    ALTER TABLE StockOut DROP CONSTRAINT CK_StockOut_PurposeCode;
END
ALTER TABLE StockOut ADD CONSTRAINT CK_StockOut_PurposeCode CHECK (PurposeCode IN ('Sale', 'WarrantyReplacement', 'Adjustment'));
");
            migrationBuilder.Sql(@"
IF EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_StockIn_PurposeCode')
BEGIN
    ALTER TABLE StockIn DROP CONSTRAINT CK_StockIn_PurposeCode;
END
ALTER TABLE StockIn ADD CONSTRAINT CK_StockIn_PurposeCode CHECK (PurposeCode IN ('Purchase', 'OpeningBalance', 'Adjustment'));
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_StockOut_PurposeCode')
BEGIN
    ALTER TABLE StockOut DROP CONSTRAINT CK_StockOut_PurposeCode;
END
ALTER TABLE StockOut ADD CONSTRAINT CK_StockOut_PurposeCode CHECK (PurposeCode IN ('Sale', 'WarrantyReplacement'));
");
            migrationBuilder.Sql(@"
IF EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_StockIn_PurposeCode')
BEGIN
    ALTER TABLE StockIn DROP CONSTRAINT CK_StockIn_PurposeCode;
END
ALTER TABLE StockIn ADD CONSTRAINT CK_StockIn_PurposeCode CHECK (PurposeCode IN ('Purchase', 'OpeningBalance'));
");
        }
    }
}
