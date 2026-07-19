using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyHangHoa.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditArchiveOperationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OperationId",
                table: "AuditArchiveManifest",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE [AuditArchiveManifest] SET [OperationId] = NEWID() WHERE [OperationId] IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "OperationId",
                table: "AuditArchiveManifest",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
            migrationBuilder.CreateIndex(
                name: "UX_AuditArchiveManifest_OperationId",
                table: "AuditArchiveManifest",
                column: "OperationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_AuditArchiveManifest_OperationId",
                table: "AuditArchiveManifest");

            migrationBuilder.DropColumn(
                name: "OperationId",
                table: "AuditArchiveManifest");
        }
    }
}
