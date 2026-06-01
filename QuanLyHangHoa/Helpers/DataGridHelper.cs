using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace QuanLyHangHoa.Helpers
{
    public static class DataGridHelper
    {
        public static readonly DependencyProperty LoadMoreCommandProperty =
            DependencyProperty.RegisterAttached(
                "LoadMoreCommand",
                typeof(ICommand),
                typeof(DataGridHelper),
                new PropertyMetadata(null, OnLoadMoreCommandChanged));

        public static ICommand GetLoadMoreCommand(DependencyObject obj)
        {
            return (ICommand)obj.GetValue(LoadMoreCommandProperty);
        }

        public static void SetLoadMoreCommand(DependencyObject obj, ICommand value)
        {
            obj.SetValue(LoadMoreCommandProperty, value);
        }

        private static void OnLoadMoreCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DataGrid dataGrid)
            {
                dataGrid.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(DataGrid_ScrollChanged));
                if (e.NewValue != null)
                {
                    dataGrid.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(DataGrid_ScrollChanged));
                }
            }
        }

        private static void DataGrid_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (sender is DataGrid dataGrid)
            {
                if (e.OriginalSource is ScrollViewer scrollViewer)
                {
                    var command = GetLoadMoreCommand(dataGrid);
                    if (command == null) return;

                    if (e.VerticalChange > 0)
                    {
                        bool shouldLoadMore = false;
                        if (scrollViewer.ScrollableHeight > 0)
                        {
                            if (scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - 50 || 
                                scrollViewer.VerticalOffset / scrollViewer.ScrollableHeight >= 0.9)
                            {
                                shouldLoadMore = true;
                            }
                        }

                        if (shouldLoadMore && command.CanExecute(null))
                        {
                            command.Execute(null);
                        }
                    }
                }
            }
        }
    }
}
