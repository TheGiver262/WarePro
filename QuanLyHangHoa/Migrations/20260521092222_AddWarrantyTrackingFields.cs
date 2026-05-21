using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyHangHoa.Migrations
{
    /// <inheritdoc />
    public partial class AddWarrantyTrackingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpectedReturnDate",
                table: "WarrantyClaim",
                type: "datetime2(0)",
                precision: 0,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ManufacturerExpectedReturnDate",
                table: "WarrantyClaim",
                type: "datetime2(0)",
                precision: 0,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManufacturerName",
                table: "WarrantyClaim",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManufacturerTrackingCode",
                table: "WarrantyClaim",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpectedReturnDate",
                table: "WarrantyClaim");

            migrationBuilder.DropColumn(
                name: "ManufacturerExpectedReturnDate",
                table: "WarrantyClaim");

            migrationBuilder.DropColumn(
                name: "ManufacturerName",
                table: "WarrantyClaim");

            migrationBuilder.DropColumn(
                name: "ManufacturerTrackingCode",
                table: "WarrantyClaim");
        }
    }
}
