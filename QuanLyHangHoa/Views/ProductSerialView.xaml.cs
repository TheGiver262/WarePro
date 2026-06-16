using System;
using System.Collections.Specialized;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using QuanLyHangHoa.ViewModels;

namespace QuanLyHangHoa.Views
{
    public partial class ProductSerialView : UserControl
    {
        public ProductSerialView()
        {
            InitializeComponent();

            Loaded += (s, e) =>
            {
                if (SerialDataGrid?.Items is INotifyCollectionChanged notifyCollection)
                {
                    notifyCollection.CollectionChanged += Items_CollectionChanged;
                }
            };

            Unloaded += (s, e) =>
            {
                if (SerialDataGrid?.Items is INotifyCollectionChanged notifyCollection)
                {
                    notifyCollection.CollectionChanged -= Items_CollectionChanged;
                }
            };
        }

        private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                if (SerialDataGrid == null) return;

                if (VisualTreeHelper.GetChildrenCount(SerialDataGrid) > 0)
                {
                    var border = VisualTreeHelper.GetChild(SerialDataGrid, 0) as Decorator;
                    var scrollViewer = border?.Child as ScrollViewer;
                    if (scrollViewer != null)
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            scrollViewer.ScrollToTop();
                        }), DispatcherPriority.Background);
                    }
                }
            }
        }
    }
}
