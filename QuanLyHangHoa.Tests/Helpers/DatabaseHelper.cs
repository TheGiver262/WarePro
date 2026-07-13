using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using System;

namespace QuanLyHangHoa.Tests.Helpers
{
    public static class DatabaseHelper
    {
        public static AppDbContext CreateContext(SqliteConnection connection)
        {
            // Disable foreign key constraints for unit tests to allow seeding dummy references
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA foreign_keys = OFF;";
                command.ExecuteNonQuery();
            }

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            return new AppDbContext(options);
        }

        public static void SeedBasicData(AppDbContext context)
        {
            context.Database.EnsureCreated();

            if (!context.Categories.Any())
            {
                context.Categories.Add(new Category { Id = 1, CategoryCode = "CAT1", DisplayName = "Default Category", IsActive = true });
            }

            if (!context.Brands.Any())
            {
                context.Brands.Add(new Brand { Id = 1, BrandCode = "BRD1", DisplayName = "Default Brand", IsActive = true });
            }

            if (!context.Units.Any())
            {
                context.Units.Add(new Unit { Id = 1, UnitCode = "PCS", DisplayName = "Pieces", IsActive = true });
            }

            if (!context.Warehouses.Any())
            {
                context.Warehouses.Add(new Warehouse { Id = 1, WarehouseCode = "WH1", DisplayName = "Main Warehouse", IsActive = true });
            }

            if (!context.AppUsers.Any())
            {
                context.AppUsers.AddRange(
                    new AppUser { Id = 1, Username = "admin", FullName = "Administrator", PasswordHash = "hash", RoleCode = "Quản trị viên", IsActive = true },
                    new AppUser { Id = 2, Username = "manager", FullName = "Manager", PasswordHash = "hash", RoleCode = "Quản lý", IsActive = true },
                    new AppUser { Id = 3, Username = "staff", FullName = "Staff Member", PasswordHash = "hash", RoleCode = "Nhân viên kho", IsActive = true }
                );
            }

            if (!context.Customers.Any())
            {
                context.Customers.Add(new Customer { Id = 1, CustomerCode = "CUST1", DisplayName = "General Customer", IsActive = true });
            }

            if (!context.Suppliers.Any())
            {
                context.Suppliers.Add(new Supplier { Id = 1, SupplierCode = "SUP1", DisplayName = "General Supplier", IsActive = true });
            }

            context.SaveChanges();
        }
    }
}
