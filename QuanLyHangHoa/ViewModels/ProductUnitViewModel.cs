using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.ViewModels
{
    public partial class ProductUnitViewModel : ObservableObject, IRefreshable
    {
        private readonly ProductUnitService _service;
        private readonly ProductService _productService;
        private readonly ReferenceDataService _refDataService;
        private readonly AppUser _currentUser;
        private readonly Action? _openUnitManagement;
        private readonly Func<bool> _canManage;
        private List<ProductUnit> _allProductUnits = new();

        [ObservableProperty] private ObservableCollection<Product> _products = new();
        [ObservableProperty] private Product? _selectedProduct;
        [ObservableProperty] private ObservableCollection<ProductUnit> _productUnits = new();
        [ObservableProperty] private ProductUnit? _selectedProductUnit;
        [ObservableProperty] private ObservableCollection<Unit> _availableUnits = new();
        [ObservableProperty] private int _selectedUnitId;
        [ObservableProperty] private decimal _conversionFactor = 1;
        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private string _searchText = string.Empty;

        public ProductUnitViewModel(
            Func<Data.AppDbContext> contextFactory,
            AppUser currentUser,
            Action? openUnitManagement = null,
            Func<bool>? canManage = null)
            : this(
                new ProductUnitService(contextFactory),
                new ProductService(contextFactory),
                new ReferenceDataService(contextFactory),
                currentUser,
                openUnitManagement,
                canManage)
        {
        }

        public ProductUnitViewModel(
            ProductUnitService service,
            ProductService productService,
            ReferenceDataService refDataService,
            AppUser currentUser,
            Action? openUnitManagement = null,
            Func<bool>? canManage = null)
        {
            ArgumentNullException.ThrowIfNull(currentUser);
            _service = service;
            _productService = productService;
            _refDataService = refDataService;
            _currentUser = currentUser;
            _openUnitManagement = openUnitManagement;
            _canManage = canManage ?? (() =>
                AuthorizationService.CanPerform(currentUser, PermissionAction.ManageMasterData));

            RefreshData();
        }

        // giữ product id đang chọn qua lần reload, rồi nạp lại units và quan hệ product-unit
        public void RefreshData()
        {
            var selectedProductId = SelectedProduct?.Id;
            Products = new ObservableCollection<Product>(
                _productService.GetAllProducts() ?? new List<Product>());
            AvailableUnits = new ObservableCollection<Unit>(
                _refDataService.GetAllUnits() ?? new List<Unit>());

            var previousSelection = SelectedProduct;
            SelectedProduct = Products.FirstOrDefault(product => product.Id == selectedProductId)
                ?? Products.FirstOrDefault();
            if (ReferenceEquals(previousSelection, SelectedProduct))
            {
                if (SelectedProduct == null)
                {
                    _allProductUnits = new List<ProductUnit>();
                    ApplyFilter();
                }
                else
                {
                    LoadProductUnits(SelectedProduct.Id);
                }
            }
        }

        partial void OnSelectedProductChanged(Product? value)
        {
            if (value != null)
                LoadProductUnits(value.Id);
            else
            {
                _allProductUnits = new List<ProductUnit>();
                ApplyFilter();
            }
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter();

        private void LoadProductUnits(int productId)
        {
            _allProductUnits = _service.GetByProductId(productId) ?? new List<ProductUnit>();
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            IEnumerable<ProductUnit> query = _allProductUnits;
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var term = SearchText.Trim();
                query = query.Where(productUnit =>
                    productUnit.Product?.DisplayName?.Contains(term, StringComparison.OrdinalIgnoreCase) == true
                    || productUnit.Product?.ProductCode?.Contains(term, StringComparison.OrdinalIgnoreCase) == true
                    || productUnit.Unit?.DisplayName?.Contains(term, StringComparison.OrdinalIgnoreCase) == true
                    || productUnit.Unit?.UnitCode?.Contains(term, StringComparison.OrdinalIgnoreCase) == true);
            }

            ProductUnits = new ObservableCollection<ProductUnit>(query);
        }

        [RelayCommand]
        private void Refresh() => RefreshData();

        private bool CanManage() => _canManage();

        // CanExecute hỗ trợ UI, nhưng handler vẫn kiểm tra lại quyền trước khi thay dữ liệu
        private bool EnsureCanManage()
        {
            if (CanManage())
                return true;

            StatusMessage = "Bạn không có quyền quản lý đơn vị quy đổi.";
            return false;
        }

        [RelayCommand(CanExecute = nameof(CanManage))]
        private void OpenAddUnitWindow()
        {
            if (EnsureCanManage())
                _openUnitManagement?.Invoke();
        }

        [RelayCommand(CanExecute = nameof(CanManage))]
        // ConversionFactor là số đơn vị cơ sở trên một đơn vị chọn; service kiểm tra dương, trùng và base-unit invariant
        private async Task Save()
        {
            if (!EnsureCanManage())
                return;

            if (SelectedProduct == null || SelectedUnitId == 0)
            {
                StatusMessage = "Chưa chọn hàng hóa hoặc đơn vị.";
                return;
            }

            try
            {
                if (SelectedProductUnit == null)
                {
                    await _service.AddAsync(new ProductUnit
                    {
                        ProductId = SelectedProduct.Id,
                        UnitId = SelectedUnitId,
                        ConversionFactor = ConversionFactor
                    }, _currentUser.Id, Guid.NewGuid());
                }
                else
                {
                    var updated = new ProductUnit
                    {
                        Id = SelectedProductUnit.Id,
                        ProductId = SelectedProductUnit.ProductId,
                        UnitId = SelectedUnitId,
                        ConversionFactor = ConversionFactor,
                        IsBaseUnit = SelectedProductUnit.IsBaseUnit,
                        IsPurchaseUnit = SelectedProductUnit.IsPurchaseUnit,
                        IsSalesUnit = SelectedProductUnit.IsSalesUnit
                    };
                    await _service.UpdateAsync(updated.Id, updated, SelectedProductUnit.RowVersion, _currentUser.Id, Guid.NewGuid());
                }

                StatusMessage = "Đã lưu đơn vị quy đổi.";
                LoadProductUnits(SelectedProduct.Id);
                Clear();
            }
            catch (DatabaseWriteConflictException)
            {
                LoadProductUnits(SelectedProduct.Id);
                Clear();
            }
            catch (Exception)
            {
                StatusMessage = DatabaseWriteUi.TechnicalErrorMessage;
            }
        }

        [RelayCommand(CanExecute = nameof(CanManage))]
        // service chặn xóa đơn vị cơ sở hoặc quan hệ cần thiết; chỉ reload sau khi xóa thành công
        private async Task Delete(ProductUnit? productUnit)
        {
            if (!EnsureCanManage())
                return;

            var target = productUnit ?? SelectedProductUnit;
            if (target == null || SelectedProduct == null)
                return;

            try
            {
                await _service.DeleteAsync(target.Id, target.RowVersion, _currentUser.Id, Guid.NewGuid());
                StatusMessage = "Đã xóa đơn vị quy đổi.";
                LoadProductUnits(SelectedProduct.Id);
                Clear();
            }
            catch (DatabaseWriteConflictException)
            {
                LoadProductUnits(SelectedProduct.Id);
                Clear();
            }
            catch (Exception)
            {
                StatusMessage = DatabaseWriteUi.TechnicalErrorMessage;
            }
        }

        private void Clear()
        {
            SelectedProductUnit = null;
            SelectedUnitId = 0;
            ConversionFactor = 1;
        }

        partial void OnSelectedProductUnitChanged(ProductUnit? value)
        {
            if (value == null)
                return;

            SelectedUnitId = value.UnitId;
            ConversionFactor = value.ConversionFactor;
        }
    }
}
