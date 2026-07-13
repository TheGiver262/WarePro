using System.IO;
using System.Reflection;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.Tests.ViewModels;

public class MasterDataDependencyUiContractTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void Product_delete_shows_structured_dependencies_and_reaches_service_mutation()
    {
        var method = ReadMethod("ProductViewModel.cs", "private void DeleteProduct", "[RelayCommand]");

        Assert.Contains("_service.GetDependencies(product.Id)", method);
        Assert.DoesNotContain("_service.HasTransactionHistory(product.Id)", method);
        Assert.Contains("dependency.Name", method);
        Assert.Contains("dependency.Count", method);
        Assert.Contains("_service.DeleteProduct(product.Id, _currentUser.Id)", method);
        Assert.Equal(1, Count(method, "return;"));
    }

    [Fact]
    public void App_user_delete_distinguishes_deactivation_from_permanent_delete()
    {
        var method = ReadMethod("AppUserViewModel.cs", "private void DeleteUser", "[RelayCommand]");

        Assert.Contains("_userService.HasDependencies(user.Id)", method);
        Assert.Contains("chuyển trạng thái người dùng sang 'Dừng'", method);
        Assert.Contains("xoá vĩnh viễn", method);
        Assert.DoesNotContain("Thao tác này không thể hoàn tác", method);
        Assert.Contains("_userService.DeleteUser(user.Id, _currentUser.Id)", method);
    }

    [Fact]
    public void App_user_service_exposes_minimal_dependency_predicate()
    {
        var method = typeof(AppUserService).GetMethod(
            "HasDependencies",
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: [typeof(int)],
            modifiers: null);

        Assert.NotNull(method);
        Assert.Equal(typeof(bool), method.ReturnType);
    }

    [Fact]
    public void Product_unit_snapshot_contains_positive_factor_constraint()
    {
        var snapshot = File.ReadAllText(Path.Combine(
            RepoRoot,
            "QuanLyHangHoa",
            "Migrations",
            "AppDbContextModelSnapshot.cs"));

        Assert.Contains("b.ToTable(\"ProductUnit\", t =>", snapshot);
        Assert.Contains(
            "t.HasCheckConstraint(\"CK_ProductUnit_ConversionFactor_Positive\", \"[ConversionFactor] > 0\")",
            snapshot);
    }

    private static string ReadMethod(string fileName, string startMarker, string nextMarker)
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot,
            "QuanLyHangHoa",
            "ViewModels",
            fileName));
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing method marker {startMarker}.");
        var end = source.IndexOf(nextMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(end > start, $"Missing next marker {nextMarker}.");
        return source[start..end];
    }

    private static int Count(string source, string value)
    {
        var count = 0;
        for (var index = 0;
             (index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0;
             index += value.Length)
        {
            count++;
        }

        return count;
    }
}
