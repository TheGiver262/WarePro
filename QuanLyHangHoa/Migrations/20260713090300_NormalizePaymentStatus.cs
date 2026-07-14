using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using QuanLyHangHoa.Data;

#nullable disable

namespace QuanLyHangHoa.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260713090300_NormalizePaymentStatus")]
public sealed class NormalizePaymentStatus : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        NormalizeTable(migrationBuilder, "SalesInvoice", "CK_SalesInvoice_PaymentStatus");
        NormalizeTable(migrationBuilder, "PurchaseInvoice", "CK_PurchaseInvoice_PaymentStatus");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        RestoreLegacyTable(migrationBuilder, "PurchaseInvoice", "CK_PurchaseInvoice_PaymentStatus");
        RestoreLegacyTable(migrationBuilder, "SalesInvoice", "CK_SalesInvoice_PaymentStatus");
    }

    private static void NormalizeTable(
        MigrationBuilder migrationBuilder,
        string table,
        string constraint)
    {
        DropConstraintIfExists(migrationBuilder, table, constraint);
        migrationBuilder.Sql($"""
            UPDATE [{table}] SET [PaymentStatus] = 'Unpaid' WHERE UPPER([PaymentStatus]) = 'UNPAID';
            UPDATE [{table}] SET [PaymentStatus] = 'PartiallyPaid' WHERE UPPER([PaymentStatus]) IN ('PARTIAL', 'PARTIALLYPAID');
            UPDATE [{table}] SET [PaymentStatus] = 'Paid' WHERE UPPER([PaymentStatus]) = 'PAID';
            UPDATE [{table}] SET [PaymentStatus] = 'Overdue' WHERE UPPER([PaymentStatus]) = 'OVERDUE';
            """);
        migrationBuilder.AddCheckConstraint(
            name: constraint,
            table: table,
            sql: "[PaymentStatus] IN ('Unpaid', 'PartiallyPaid', 'Paid', 'Overdue')");
    }

    private static void RestoreLegacyTable(
        MigrationBuilder migrationBuilder,
        string table,
        string constraint)
    {
        DropConstraintIfExists(migrationBuilder, table, constraint);
        migrationBuilder.Sql($"""
            UPDATE [{table}] SET [PaymentStatus] = 'Unpaid' WHERE UPPER([PaymentStatus]) = 'UNPAID';
            UPDATE [{table}] SET [PaymentStatus] = 'Partial' WHERE UPPER([PaymentStatus]) IN ('PARTIAL', 'PARTIALLYPAID');
            UPDATE [{table}] SET [PaymentStatus] = 'Paid' WHERE UPPER([PaymentStatus]) = 'PAID';
            UPDATE [{table}] SET [PaymentStatus] = 'Overdue' WHERE UPPER([PaymentStatus]) = 'OVERDUE';
            """);
        migrationBuilder.AddCheckConstraint(
            name: constraint,
            table: table,
            sql: "[PaymentStatus] IN ('Unpaid', 'Partial', 'Paid', 'Overdue')");
    }

    private static void DropConstraintIfExists(
        MigrationBuilder migrationBuilder,
        string table,
        string constraint)
    {
        migrationBuilder.Sql($"""
            IF EXISTS
            (
                SELECT 1
                FROM sys.check_constraints
                WHERE [name] = N'{constraint}'
                  AND [parent_object_id] = OBJECT_ID(N'[{table}]')
            )
            BEGIN
                ALTER TABLE [{table}] DROP CONSTRAINT [{constraint}];
            END;
            """);
    }
}
