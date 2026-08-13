using System.IO;

namespace QuanLyHangHoa.Tests.ViewModels;

public class UserFacingErrorBoundaryContractTests
{
    [Fact]
    public void Touched_view_models_do_not_expose_exception_messages_to_users()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        foreach (var file in new[]
                 {
                     "BrandViewModel.cs",
                     "CategoryViewModel.cs",
                     "UnitViewModel.cs",
                     "CustomerViewModel.cs",
                     "SupplierViewModel.cs",
                     "InventoryViewModel.cs",
                     "WarrantyViewModel.cs"
                 })
        {
            var lines = File.ReadAllLines(Path.Combine(repoRoot, "QuanLyHangHoa", "ViewModels", file));
            for (var index = 0; index < lines.Length; index++)
            {
                if (!lines[index].Contains("ex.Message", StringComparison.Ordinal)
                    || lines[index].Contains("Debug.WriteLine", StringComparison.Ordinal))
                {
                    continue;
                }

                var catchContext = string.Join(
                    Environment.NewLine,
                    lines.Skip(Math.Max(0, index - 6)).Take(Math.Min(7, index + 1)));
                Assert.Contains("catch (InventoryDomainException ex)", catchContext, StringComparison.Ordinal);
            }
        }
    }
}
