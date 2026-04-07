using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace QuanLyHangHoa.Migrations
{
    /// <inheritdoc />
    public partial class SeedMoreProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "BrandId", "CategoryId", "IsDeleted", "Name", "Notes", "Origin", "Quantity", "UnitId", "UnitPrice", "WarrantyMonths" },
                values: new object[,]
                {
                    { 6, 2, 2, false, "Bàn phím cơ Logitech G Pro X", "Bàn phím TKL chuyên eSports", "Trung Quốc", 60, 1, 2500000m, 24 },
                    { 7, 1, 3, false, "Màn hình cong Dell S3221QS", "Màn hình 4K 32 inch", "Mỹ", 25, 1, 11500000m, 36 },
                    { 8, 1, 1, false, "Laptop Dell Inspiron 15", "Laptop văn phòng quốc dân", "Mỹ", 40, 1, 18000000m, 12 },
                    { 9, 3, 4, false, "Tai nghe không dây Sony WH-1000XM4", "Chống ồn chủ động đỉnh cao", "Nhật Bản", 70, 1, 6500000m, 12 },
                    { 10, 2, 2, false, "Chuột không dây Logitech MX Master 3S", "Dòng chuột làm việc chuyên nghiệp", "Trung Quốc", 100, 1, 2300000m, 12 },
                    { 11, 2, 2, false, "Bàn phím không dây Logitech MX Keys", "Thiết kế mỏng, gõ êm ái", "Trung Quốc", 55, 1, 2200000m, 12 },
                    { 12, 3, 4, false, "Loa Bluetooth Sony SRS-XB13", "Nhỏ gọn, âm thanh Extra Bass", "Trung Quốc", 120, 1, 1200000m, 12 },
                    { 13, 1, 3, false, "Màn hình Dell Alienware AW2521H", "Màn hình Gaming 360Hz", "Mỹ", 15, 1, 14000000m, 36 },
                    { 14, 3, 2, false, "Máy ảnh Sony Alpha A7 III", "Máy ảnh Mirrorless Full-frame", "Nhật Bản", 10, 1, 45000000m, 24 },
                    { 15, 1, 1, false, "Laptop Dell Alienware m15 R7", "Siêu phẩm laptop gaming 2026", "Mỹ", 5, 1, 65000000m, 24 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 15);
        }
    }
}
