# Approved Code and Styling Patterns

Reusable C# and XAML patterns verified for the ProductManagement project.

## Pattern 1: XAML Container and Button Standardization
XAML structure matching the 3-row, `MaxWidth="1600"`, `ProMaxIconButtonStyle` standard:

```xml
<Grid MaxWidth="1600" HorizontalAlignment="Stretch" Margin="16">
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/> <!-- Header -->
        <RowDefinition Height="Auto"/> <!-- Search & Filters -->
        <RowDefinition Height="MaxHeight"/> <!-- DataGrid -->
    </Grid.RowDefinitions>
    
    <!-- Row 0: Header -->
    <materialDesign:Card Grid.Row="0" Padding="12" Margin="0,0,0,12" Background="{StaticResource SurfaceMutedBrush}">
        <Grid>
            <TextBlock Text="DANH SÁCH SẢN PHẨM" Style="{StaticResource MaterialDesignHeadline5TextBlock}"/>
            <StackPanel HorizontalAlignment="Right" Orientation="Horizontal">
                <Button Style="{StaticResource ProMaxIconButtonStyle}" Command="{Binding AddCommand}"/>
                <Button Style="{StaticResource ProMaxIconButtonStyle}" Command="{Binding ExportCommand}"/>
            </StackPanel>
        </Grid>
    </materialDesign:Card>
</Grid>
```

## Pattern 2: Dynamic Excel Export with ClosedXML
Dynamic row export on current view thread:

```csharp
private void Export()
{
    if (InventoryItems == null || !InventoryItems.Any()) return;

    var saveFileDialog = new SaveFileDialog
    {
        Filter = "Excel Worksheets (*.xlsx)|*.xlsx",
        FileName = "BaoCaoTonKho.xlsx"
    };

    if (saveFileDialog.ShowDialog() == true)
    {
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("TonKho");
            
            // Header
            worksheet.Cell(1, 1).Value = "MÃ SẢN PHẨM";
            worksheet.Cell(1, 2).Value = "TÊN SẢN PHẨM";
            // ... styling header in anthracite #4A5568 with white text
            
            // Data Rows (Dynamic collection)
            int row = 2;
            foreach (var item in InventoryItems)
            {
                worksheet.Cell(row, 1).Value = item.ProductCode;
                worksheet.Cell(row, 2).Value = item.ProductName;
                row++;
            }
            
            worksheet.Columns().AdjustToContents();
            workbook.SaveAs(saveFileDialog.FileName);
        }
    }
}
```
