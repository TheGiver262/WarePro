using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace QuanLyHangHoa.Migrations
{
    /// <inheritdoc />
    public partial class SeedSampleData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AppUsers",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "PasswordHash" },
                values: new object[] { new DateTime(2026, 4, 30, 8, 45, 36, 59, DateTimeKind.Utc).AddTicks(1219), "$2a$11$SrDIlwwS8m16cTGIWXVBd.7GEtdHs0VlCdEeG5AQYYw4tv5u97Y2u" });

            migrationBuilder.InsertData(
                table: "Customers",
                columns: new[] { "Id", "Address", "CustomerCode", "DisplayName", "Email", "IsActive", "Phone" },
                values: new object[,]
                {
                    { 1, "Đà Nẵng", "CUS01", "Nguyễn Văn A", "nguyenvana@gmail.com", true, "0909090909" },
                    { 2, "Hải Phòng", "CUS02", "Trần Thị B", "tranthib@gmail.com", true, "0808080808" }
                });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "BrandId", "CategoryId", "DefaultPrice", "DefaultUnitId", "DisplayName", "IsActive", "IsSerialTracked", "OriginCountry", "ProductCode", "UnitId", "WarrantyPeriodMonths" },
                values: new object[,]
                {
                    { 1, 1, 1, 15000000m, 1, "Laptop Dell Inspiron 15", true, true, "Trung Quốc", "PROD01", null, 12 },
                    { 2, 2, 2, 6000000m, 1, "Tai nghe Sony WH-1000XM4", true, false, "Malaysia", "PROD02", null, 12 }
                });

            migrationBuilder.InsertData(
                table: "Suppliers",
                columns: new[] { "Id", "Address", "DisplayName", "Email", "IsActive", "Phone", "SupplierCode" },
                values: new object[,]
                {
                    { 1, "Hà Nội", "Công ty TNHH Công Nghệ A", "contact@tech-a.vn", true, "0123456789", "SUP01" },
                    { 2, "TP. HCM", "Nhà Phân Phối B", "sales@distributor-b.com", true, "0987654321", "SUP02" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Customers",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Customers",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Suppliers",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Suppliers",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.UpdateData(
                table: "AppUsers",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "PasswordHash" },
                values: new object[] { new DateTime(2026, 4, 29, 19, 49, 42, 959, DateTimeKind.Utc).AddTicks(2634), "$2a$11$EhOhp6Ou8HHYkOQVfvMPueoumS/rQ9SCd61tFsTvCkF0ufkWifzMO" });
        }
    }
}
