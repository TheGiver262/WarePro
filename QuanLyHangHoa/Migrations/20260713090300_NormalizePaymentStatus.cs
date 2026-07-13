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
        migrationBuilder.DropCheckConstraint(name: constraint, table: table);
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
        migrationBuilder.DropCheckConstraint(name: constraint, table: table);
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
}
