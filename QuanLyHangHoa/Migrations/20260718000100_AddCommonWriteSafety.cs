using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyHangHoa.Migrations
{
    /// <inheritdoc />
    public partial class AddCommonWriteSafety : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "WarrantyCoverage",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "WarrantyClaim",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Warehouse",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Unit",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Supplier",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "StockTransferLine",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "StockTransfer",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "StockOutLine",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "StockOut",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "StockInLine",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "StockIn",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "StockCountSession",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "StockCountLine",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "StockBalance",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "StockAdjustmentLine",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "StockAdjustment",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "SalesInvoiceLine",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "SalesInvoice",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PurchaseInvoiceLine",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PurchaseInvoice",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ProductUnit",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ProductSerial",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Product",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Customer",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Category",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Brand",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AuditArchiveManifest",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AppUser",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateTable(
                name: "__WareProClientSession",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MachineName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ProcessId = table.Column<int>(type: "int", nullable: false),
                    AppVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK___WareProClientSession", x => x.SessionId);
                });

            migrationBuilder.CreateIndex(
                name: "IX___WareProClientSession_LastSeenUtc",
                table: "__WareProClientSession",
                column: "LastSeenUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "__WareProClientSession");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "WarrantyCoverage");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "WarrantyClaim");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Warehouse");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Unit");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Supplier");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "StockTransferLine");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "StockTransfer");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "StockOutLine");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "StockOut");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "StockInLine");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "StockIn");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "StockCountSession");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "StockCountLine");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "StockBalance");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "StockAdjustmentLine");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "StockAdjustment");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "SalesInvoiceLine");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "SalesInvoice");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PurchaseInvoiceLine");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PurchaseInvoice");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ProductUnit");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ProductSerial");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Customer");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Category");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Brand");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AuditArchiveManifest");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AppUser");
        }
    }
}
