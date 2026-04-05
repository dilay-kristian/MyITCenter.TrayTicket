using System.Windows;
using System.Windows.Input;
using MyitCenter.TrayTicketTool.Services;

namespace MyitCenter.TrayTicketTool.Views;

public partial class WindowSelectionDialog : Window
{
    public WindowInfo? SelectedWindow { get; private set; }

    public WindowSelectionDialog(IList<WindowInfo> windows)
    {
        InitializeComponent();

        var items = windows
            .Where(w => !string.IsNullOrWhiteSpace(w.Title))
            .Select(w => new WindowListItem { Info = w, DisplayText = $"{w.Title}  ({w.ProcessName})" })
            .ToList();

        WindowList.ItemsSource = items;
        if (items.Count > 0)
            WindowList.SelectedIndex = 0;
    }

    private void Select_Click(object sender, RoutedEventArgs e)
    {
        if (WindowList.SelectedItem is WindowListItem item)
        {
            SelectedWindow = item.Info;
            DialogResult = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void WindowList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        Select_Click(sender, e);
    }

    private class WindowListItem
    {
        public WindowInfo Info { get; set; } = null!;
        public string DisplayText { get; set; } = string.Empty;
    }
}
