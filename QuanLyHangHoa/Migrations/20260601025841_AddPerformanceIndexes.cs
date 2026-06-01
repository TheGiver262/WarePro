using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyHangHoa.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_StockOut_ExportDate",
                table: "StockOut",
                column: "ExportDate");

            migrationBuilder.CreateIndex(
                name: "IX_StockIn_ImportDate",
                table: "StockIn",
                column: "ImportDate");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoice_InvoiceDate",
                table: "SalesInvoice",
                column: "InvoiceDate");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoice_InvoiceDate",
                table: "PurchaseInvoice",
                column: "InvoiceDate");

            migrationBuilder.CreateIndex(
                name: "IX_ProductSerial_CurrentStatus",
                table: "ProductSerial",
                column: "CurrentStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockOut_ExportDate",
                table: "StockOut");

            migrationBuilder.DropIndex(
                name: "IX_StockIn_ImportDate",
                table: "StockIn");

            migrationBuilder.DropIndex(
                name: "IX_SalesInvoice_InvoiceDate",
                table: "SalesInvoice");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoice_InvoiceDate",
                table: "PurchaseInvoice");

            migrationBuilder.DropIndex(
                name: "IX_ProductSerial_CurrentStatus",
                table: "ProductSerial");
        }
    }
}
