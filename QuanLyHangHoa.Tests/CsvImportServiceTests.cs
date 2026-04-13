using System.IO;
using System.Linq;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services.DataImport;
using Xunit;

namespace QuanLyHangHoa.Tests
{
    public class CsvImportServiceTests
    {
        [Fact]
        public void Import_ValidCsv_ReturnsImportedItems()
        {
            // Arrange
            var service = new CsvImportService();
            string csvContent = "Name\nDanh Muc 1\nDanh Muc 2\n";
            string tempFile = Path.GetTempFileName() + ".csv";
            File.WriteAllText(tempFile, csvContent);

            try
            {
                // Act
                var result = service.Import<Category>(tempFile);

                // Assert
                Assert.NotNull(result);
                Assert.Equal(2, result.ImportedItems.Count);
                Assert.Empty(result.Errors);
                Assert.Equal("Danh Muc 1", result.ImportedItems[0].Name);
                Assert.Equal("Danh Muc 2", result.ImportedItems[1].Name);
            }
            finally
            {
                // Cleanup
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact]
        public void Import_InvalidCsv_RecordsError()
        {
            // Arrange
            var service = new CsvImportService();
            string csvContent = "Id,Quantity,UnitPrice\ninvalid_id,10,100\n2,invalid_quantity,200\n";
            string tempFile = Path.GetTempFileName() + ".csv";
            File.WriteAllText(tempFile, csvContent);

            try
            {
                // Act
                var result = service.Import<Product>(tempFile);

                // Assert
                // It should fail to parse 'invalid_id' as int, or 'invalid_quantity'
                Assert.NotNull(result);
                // With CsvHelper, a fail on one row means the row is skipped or it throws.
                // Our implementation catches per row and adds to result.Errors
                Assert.NotEmpty(result.Errors);
                Assert.True(result.Errors.Count >= 2);
            }
            finally
            {
                // Cleanup
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }
    }
}
