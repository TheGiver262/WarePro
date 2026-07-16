namespace QuanLyHangHoa.ViewModels
{
    // ViewModel được cache triển khai contract này để MainViewModel nạp lại dữ liệu khi người dùng quay lại màn hình
    public interface IRefreshable
    {
        void RefreshData();
    }
}
