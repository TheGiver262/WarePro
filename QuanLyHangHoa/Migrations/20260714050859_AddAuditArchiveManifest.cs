using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyHangHoa.Migrations;

public partial class AddAuditArchiveManifest : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_AuditLog_PerformedBy",
            table: "AuditLog");

        migrationBuilder.AlterColumn<int>(
            name: "PerformedBy",
            table: "AuditLog",
            type: "int",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "int");

        migrationBuilder.CreateTable(
            name: "AuditArchiveManifest",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                ActorId = table.Column<int>(type: "int", nullable: false),
                RangeStartUtc = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false),
                RangeEndUtc = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false),
                RowCount = table.Column<int>(type: "int", nullable: false),
                FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                Sha256Hash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuditArchiveManifest", x => x.Id);
                table.ForeignKey(
                    name: "FK_AuditArchiveManifest_AppUser_ActorId",
                    column: x => x.ActorId,
                    principalTable: "AppUser",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AuditArchiveManifest_ActorId",
            table: "AuditArchiveManifest",
            column: "ActorId");

        migrationBuilder.CreateIndex(
            name: "IX_AuditArchiveManifest_CreatedAtUtc",
            table: "AuditArchiveManifest",
            column: "CreatedAtUtc");

        migrationBuilder.AddForeignKey(
            name: "FK_AuditLog_PerformedBy",
            table: "AuditLog",
            column: "PerformedBy",
            principalTable: "AppUser",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_AuditLog_PerformedBy",
            table: "AuditLog");

        migrationBuilder.DropTable(name: "AuditArchiveManifest");
        migrationBuilder.Sql("DELETE FROM [AuditLog] WHERE [PerformedBy] IS NULL;");

        migrationBuilder.AlterColumn<int>(
            name: "PerformedBy",
            table: "AuditLog",
            type: "int",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "int",
            oldNullable: true);

        migrationBuilder.AddForeignKey(
            name: "FK_AuditLog_PerformedBy",
            table: "AuditLog",
            column: "PerformedBy",
            principalTable: "AppUser",
            principalColumn: "Id");
    }
}