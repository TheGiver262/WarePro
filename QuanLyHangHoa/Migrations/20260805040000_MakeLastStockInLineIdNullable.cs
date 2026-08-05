using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyHangHoa.Migrations
{
    /// <inheritdoc />
    public partial class MakeLastStockInLineIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bỏ FK cũ (NOT NULL) trước khi alter column
            migrationBuilder.DropForeignKey(
                name: "FK_ProductSerial_LastStockInLine",
                table: "ProductSerials");

            // Thay đổi cột từ NOT NULL sang nullable
            migrationBuilder.AlterColumn<int>(
                name: "LastStockInLineId",
                table: "ProductSerials",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: false);

            // Tái tạo FK với nullable (ON DELETE SET NULL)
            migrationBuilder.AddForeignKey(
                name: "FK_ProductSerial_LastStockInLine",
                table: "ProductSerials",
                column: "LastStockInLineId",
                principalTable: "StockInLines",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductSerial_LastStockInLine",
                table: "ProductSerials");

            // Gán placeholder cho bất kỳ row null nào trước khi đổi sang NOT NULL
            migrationBuilder.Sql(
                @"UPDATE [ProductSerials]
                  SET [LastStockInLineId] = (
                      SELECT TOP 1 [Id] FROM [StockInLines] ORDER BY [Id] DESC
                  )
                  WHERE [LastStockInLineId] IS NULL");

            migrationBuilder.AlterColumn<int>(
                name: "LastStockInLineId",
                table: "ProductSerials",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductSerial_LastStockInLine",
                table: "ProductSerials",
                column: "LastStockInLineId",
                principalTable: "StockInLines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
