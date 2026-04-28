using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.ViewModels
{
    public partial class AuditQueryViewModel : ObservableObject
    {
        private readonly Func<IReadOnlyList<Product>> _productLoader;
        private readonly Func<int, IReadOnlyList<AuditTimelineEntry>> _productLedgerLoader;
        private readonly Func<Guid, IReadOnlyList<AuditTimelineEntry>> _documentTimelineLoader;

        [ObservableProperty] private ObservableCollection<Product> _availableProducts = new();
        [ObservableProperty] private Product? _selectedProduct;
        [ObservableProperty] private string _documentIdText = string.Empty;
        [ObservableProperty] private ObservableCollection<AuditTimelineEntry> _entries = new();
        [ObservableProperty] private string _reportTitle = "Lich su ton kho";
        [ObservableProperty] private string _statusMessage = string.Empty;

        public AuditQueryViewModel()
            : this(
                new ProductService().GetAllProducts,
                new AuditQueryService().GetProductLedger,
                new AuditQueryService().GetDocumentTimeline)
        {
        }

        public AuditQueryViewModel(
            Func<IReadOnlyList<Product>> productLoader,
            Func<int, IReadOnlyList<AuditTimelineEntry>> productLedgerLoader,
            Func<Guid, IReadOnlyList<AuditTimelineEntry>> documentTimelineLoader)
        {
            _productLoader = productLoader;
            _productLedgerLoader = productLedgerLoader;
            _documentTimelineLoader = documentTimelineLoader;
            AvailableProducts = new ObservableCollection<Product>(_productLoader());
        }

        [RelayCommand]
        private void LoadProductLedger()
        {
            if (SelectedProduct == null)
            {
                Entries.Clear();
                StatusMessage = "Vui long chon san pham.";
                return;
            }

            var loaded = _productLedgerLoader(SelectedProduct.Id);
            Entries = new ObservableCollection<AuditTimelineEntry>(loaded);
            ReportTitle = $"Lich su san pham: {SelectedProduct.Name}";
            StatusMessage = $"Da tai {Entries.Count} dong lich su.";
        }

        [RelayCommand]
        private void LoadDocumentTimeline()
        {
            if (!Guid.TryParse(DocumentIdText, out var documentId))
            {
                Entries.Clear();
                StatusMessage = "DocumentId khong hop le.";
                return;
            }

            var loaded = _documentTimelineLoader(documentId);
            Entries = new ObservableCollection<AuditTimelineEntry>(loaded);
            ReportTitle = $"Timeline document: {documentId}";
            StatusMessage = $"Da tai {Entries.Count} dong timeline.";
        }
    }
}
