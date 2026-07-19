using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace QuanLyHangHoa.Models;

public partial class StockCountLine : ObservableObject
{
    public int Id { get; set; }

    public int SessionId { get; set; }

    public int ProductId { get; set; }

    public decimal SystemQuantity { get; set; }

    // variance luôn bằng số đếm thực tế trừ snapshot hệ thống tại lúc lập phiên kiểm kê
    private decimal _countedQuantity;
    public decimal CountedQuantity
    {
        get => _countedQuantity;
        set
        {
            if (SetProperty(ref _countedQuantity, value))
            {
                VarianceQuantity = value - SystemQuantity;
                OnPropertyChanged(nameof(VarianceQuantity));
                OnPropertyChanged(nameof(ShowSerialButton));
            }
        }
    }

    private decimal _varianceQuantity;
    public decimal VarianceQuantity
    {
        get => _varianceQuantity;
        set
        {
            if (SetProperty(ref _varianceQuantity, value))
            {
                OnPropertyChanged(nameof(ShowSerialButton));
            }
        }
    }

    private string? _serialNumbers;
    public string? SerialNumbers
    {
        get => _serialNumbers;
        set => SetProperty(ref _serialNumbers, value);
    }

    // sản phẩm theo serial chỉ cần mở đối soát serial khi số đếm khác hệ thống
    public bool ShowSerialButton => Product != null && Product.IsSerialTracked && (CountedQuantity != SystemQuantity);

    private Product _product = null!;
    public virtual Product Product
    {
        get => _product;
        set
        {
            if (SetProperty(ref _product, value))
            {
                OnPropertyChanged(nameof(ShowSerialButton));
            }
        }
    }

    public virtual StockCountSession Session { get; set; } = null!;
    public byte[] RowVersion { get; set; } = [];

}
