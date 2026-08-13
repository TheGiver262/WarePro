namespace QuanLyHangHoa.Tests.ViewModels;

public sealed class MasterDataEditIsolationContractTests
{
    private static readonly string Root = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Theory]
    [InlineData("CategoryViewModel.cs", "vm.ApplyTo(category)")]
    [InlineData("UnitViewModel.cs", "vm.ApplyTo(unit)")]
    [InlineData("CustomerViewModel.cs", "vm.ApplyTo(customer)")]
    [InlineData("SupplierViewModel.cs", "vm.ApplyTo(supplier)")]
    public void Edit_commands_do_not_apply_dialog_values_to_bound_entities(
        string fileName,
        string forbiddenMutation)
    {
        var source = File.ReadAllText(Path.Combine(Root, "QuanLyHangHoa", "ViewModels", fileName));

        Assert.DoesNotContain(forbiddenMutation, source, StringComparison.Ordinal);
        Assert.Contains("vm.ApplyTo(updated)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Brand_edit_builds_a_detached_update_entity()
    {
        var source = File.ReadAllText(Path.Combine(
            Root, "QuanLyHangHoa", "ViewModels", "BrandViewModel.cs"));

        Assert.Contains("var updated = new Brand", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedBrand.BrandCode =", source, StringComparison.Ordinal);
    }
}
