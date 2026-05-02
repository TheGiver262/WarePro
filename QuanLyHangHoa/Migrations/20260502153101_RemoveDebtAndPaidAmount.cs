using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyHangHoa.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDebtAndPaidAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoicePayments");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "PurchaseInvoices");

            migrationBuilder.UpdateData(
                table: "AppUsers",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "PasswordHash" },
                values: new object[] { new DateTime(2026, 5, 2, 15, 30, 58, 110, DateTimeKind.Utc).AddTicks(9380), "$2a$11$wd5deuDEeLh0Yu6nmca0xe3vw23FHJXauZ3r6ZX3WGpqLbVi5LqbS" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Notes",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "PurchaseInvoices");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "SalesInvoices",
                newName: "DueDate");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "PurchaseInvoices",
                newName: "DueDate");

            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                table: "SalesInvoices",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "SalesInvoices",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                table: "PurchaseInvoices",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "PurchaseInvoices",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "InvoicePayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PurchaseInvoiceId = table.Column<int>(type: "INTEGER", nullable: true),
                    SalesInvoiceId = table.Column<int>(type: "INTEGER", nullable: true),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PaymentMethod = table.Column<string>(type: "TEXT", nullable: false),
                    ReceivedBy = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoicePayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoicePayments_PurchaseInvoices_PurchaseInvoiceId",
                        column: x => x.PurchaseInvoiceId,
                        principalTable: "PurchaseInvoices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InvoicePayments_SalesInvoices_SalesInvoiceId",
                        column: x => x.SalesInvoiceId,
                        principalTable: "SalesInvoices",
                        principalColumn: "Id");
                });

            migrationBuilder.UpdateData(
                table: "AppUsers",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "PasswordHash" },
                values: new object[] { new DateTime(2026, 4, 30, 8, 45, 36, 59, DateTimeKind.Utc).AddTicks(1219), "$2a$11$SrDIlwwS8m16cTGIWXVBd.7GEtdHs0VlCdEeG5AQYYw4tv5u97Y2u" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoicePayments_PurchaseInvoiceId",
                table: "InvoicePayments",
                column: "PurchaseInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoicePayments_SalesInvoiceId",
                table: "InvoicePayments",
                column: "SalesInvoiceId");
        }
    }
}
