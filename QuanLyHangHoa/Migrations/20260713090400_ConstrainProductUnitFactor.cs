using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using QuanLyHangHoa.Data;

#nullable disable

namespace QuanLyHangHoa.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260713090400_ConstrainProductUnitFactor")]
public sealed class ConstrainProductUnitFactor : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS
            (
                SELECT 1
                FROM [ProductUnit] AS pu
                LEFT JOIN [Product] AS p ON p.[Id] = pu.[ProductId]
                WHERE pu.[ConversionFactor] <= 0
                  AND
                  (
                      p.[Id] IS NULL
                      OR pu.[IsBaseUnit] <> 1
                      OR pu.[UnitId] <> p.[DefaultUnitId]
                  )
            )
            BEGIN
                THROW 51000,
                    'Ambiguous non-positive ProductUnit.ConversionFactor. Migration stopped without normalization. Diagnostic SQL: SELECT pu.Id, pu.ProductId, pu.UnitId, pu.ConversionFactor, pu.IsBaseUnit, p.DefaultUnitId FROM ProductUnit pu LEFT JOIN Product p ON p.Id = pu.ProductId WHERE pu.ConversionFactor <= 0 AND (p.Id IS NULL OR pu.IsBaseUnit <> 1 OR pu.UnitId <> p.DefaultUnitId);',
                    1;
            END;

            UPDATE pu
            SET pu.[ConversionFactor] = 1
            FROM [ProductUnit] AS pu
            INNER JOIN [Product] AS p ON p.[Id] = pu.[ProductId]
            WHERE pu.[ConversionFactor] <= 0
              AND pu.[IsBaseUnit] = 1
              AND pu.[UnitId] = p.[DefaultUnitId];
            """);

        DropConstraintIfExists(migrationBuilder);

        migrationBuilder.AddCheckConstraint(
            name: "CK_ProductUnit_ConversionFactor_Positive",
            table: "ProductUnit",
            sql: "[ConversionFactor] > 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        DropConstraintIfExists(migrationBuilder);
    }

    private static void DropConstraintIfExists(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS
            (
                SELECT 1
                FROM sys.check_constraints
                WHERE [name] = N'CK_ProductUnit_ConversionFactor_Positive'
                  AND [parent_object_id] = OBJECT_ID(N'[ProductUnit]')
            )
            BEGIN
                ALTER TABLE [ProductUnit]
                    DROP CONSTRAINT [CK_ProductUnit_ConversionFactor_Positive];
            END;
            """);
    }
}
