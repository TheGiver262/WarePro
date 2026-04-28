using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyHangHoa.Migrations
{
    /// <inheritdoc />
    public partial class WarrantyClaimPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WarrantyCoverages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductSerialId = table.Column<int>(type: "INTEGER", nullable: false),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: false),
                    SalesInvoiceId = table.Column<int>(type: "INTEGER", nullable: true),
                    WarrantyStartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    WarrantyEndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CoverageStatus = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarrantyCoverages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarrantyCoverages_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarrantyCoverages_ProductSerials_ProductSerialId",
                        column: x => x.ProductSerialId,
                        principalTable: "ProductSerials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarrantyCoverages_SalesInvoices_SalesInvoiceId",
                        column: x => x.SalesInvoiceId,
                        principalTable: "SalesInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WarrantyClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClaimCode = table.Column<string>(type: "TEXT", nullable: false),
                    WarrantyCoverageId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductSerialId = table.Column<int>(type: "INTEGER", nullable: false),
                    ReplacementSerialId = table.Column<int>(type: "INTEGER", nullable: true),
                    ReplacementStockOutId = table.Column<int>(type: "INTEGER", nullable: true),
                    ReceivedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ClaimStatus = table.Column<string>(type: "TEXT", nullable: false),
                    ProblemDescription = table.Column<string>(type: "TEXT", nullable: false),
                    TechnicalConclusion = table.Column<string>(type: "TEXT", nullable: false),
                    ManufacturerResult = table.Column<string>(type: "TEXT", nullable: false),
                    RejectionReason = table.Column<string>(type: "TEXT", nullable: false),
                    ProcessingNote = table.Column<string>(type: "TEXT", nullable: false),
                    ApprovedBy = table.Column<int>(type: "INTEGER", nullable: true),
                    ProcessedBy = table.Column<int>(type: "INTEGER", nullable: true),
                    ClosedDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarrantyClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarrantyClaims_ProductSerials_ProductSerialId",
                        column: x => x.ProductSerialId,
                        principalTable: "ProductSerials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarrantyClaims_ProductSerials_ReplacementSerialId",
                        column: x => x.ReplacementSerialId,
                        principalTable: "ProductSerials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WarrantyClaims_StockOuts_ReplacementStockOutId",
                        column: x => x.ReplacementStockOutId,
                        principalTable: "StockOuts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WarrantyClaims_WarrantyCoverages_WarrantyCoverageId",
                        column: x => x.WarrantyCoverageId,
                        principalTable: "WarrantyCoverages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WarrantyClaims_ClaimCode",
                table: "WarrantyClaims",
                column: "ClaimCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WarrantyClaims_ProductSerialId",
                table: "WarrantyClaims",
                column: "ProductSerialId");

            migrationBuilder.CreateIndex(
                name: "IX_WarrantyClaims_ReplacementSerialId",
                table: "WarrantyClaims",
                column: "ReplacementSerialId");

            migrationBuilder.CreateIndex(
                name: "IX_WarrantyClaims_ReplacementStockOutId",
                table: "WarrantyClaims",
                column: "ReplacementStockOutId");

            migrationBuilder.CreateIndex(
                name: "IX_WarrantyClaims_WarrantyCoverageId",
                table: "WarrantyClaims",
                column: "WarrantyCoverageId");

            migrationBuilder.CreateIndex(
                name: "IX_WarrantyCoverages_CustomerId",
                table: "WarrantyCoverages",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_WarrantyCoverages_ProductSerialId",
                table: "WarrantyCoverages",
                column: "ProductSerialId");

            migrationBuilder.CreateIndex(
                name: "IX_WarrantyCoverages_SalesInvoiceId",
                table: "WarrantyCoverages",
                column: "SalesInvoiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WarrantyClaims");

            migrationBuilder.DropTable(
                name: "WarrantyCoverages");
        }
    }
}
