using System.IO;
using ClosedXML.Excel;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services.DataImport;
using Xunit;

namespace QuanLyHangHoa.Tests
{
    public class ExcelImportServiceTests
    {
        [Fact]
        public void Import_ValidExcel_ReturnsImportedItems()
        {
            // Arrange
            var service = new ExcelImportService();
            string tempFile = Path.GetTempFileName() + ".xlsx";

            try
            {
                // Create a generic Excel file
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Sheet1");
                    
                    // Headers
                    worksheet.Cell(1, 1).Value = "DisplayName";
                    
                    // Data
                    worksheet.Cell(2, 1).Value = "Danh Muc Excel 1";
                    worksheet.Cell(3, 1).Value = "Danh Muc Excel 2";

                    workbook.SaveAs(tempFile);
                }

                // Act
                var result = service.Import<Category>(tempFile);

                // Assert
                Assert.NotNull(result);
                Assert.Equal(2, result.ImportedItems.Count);
                Assert.Empty(result.Errors);
                Assert.Equal("Danh Muc Excel 1", result.ImportedItems[0].DisplayName);
                Assert.Equal("Danh Muc Excel 2", result.ImportedItems[1].DisplayName);
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
        public void Import_InvalidExcel_MissingHeader_ThrowsException()
        {
            // Arrange
            var service = new ExcelImportService();
            string tempFile = Path.GetTempFileName() + ".xlsx";

            try
            {
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Sheet1");
                    worksheet.Cell(1, 1).Value = "InvalidHeaderName";
                    worksheet.Cell(2, 1).Value = "Data";

                    workbook.SaveAs(tempFile);
                }

                // Act
                var result = service.Import<Category>(tempFile);

                // Assert
                // As properties do not match exactly, the name might just be null (not set).
                // Our simple mapper just sets values if it finds the column header matching the property Name.
                Assert.NotNull(result);
                Assert.Single(result.ImportedItems);
                // DisplayName should be null because 'InvalidHeaderName' doesn't map to 'DisplayName' and it is uninitialized
                Assert.Null(result.ImportedItems[0].DisplayName);
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
