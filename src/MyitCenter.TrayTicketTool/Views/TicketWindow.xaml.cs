using System.Windows;
using System.Windows.Input;
using MyitCenter.TrayTicketTool.Services;
using MyitCenter.TrayTicketTool.ViewModels;

namespace MyitCenter.TrayTicketTool.Views;

public partial class TicketWindow : Window
{
    private readonly TicketViewModel _viewModel;

    public TicketWindow()
    {
        InitializeComponent();
        _viewModel = new TicketViewModel();
        DataContext = _viewModel;

        _viewModel.RequestClose += () => Close();
        _viewModel.RequestHideWindow += () => Dispatcher.Invoke(() => Hide());
        _viewModel.RequestShowWindow += () => Dispatcher.Invoke(() =>
        {
            Show();
            Activate();
        });

        _viewModel.RequestRegionSelection += () =>
        {
            return Dispatcher.Invoke(() =>
            {
                var regionWindow = new RegionSelectionWindow();
                var result = regionWindow.ShowDialog();
                if (result == true && regionWindow.SelectedRegion.HasValue)
                {
                    var r = regionWindow.SelectedRegion.Value;
                    return (System.Drawing.Rectangle?)new System.Drawing.Rectangle(
                        (int)r.X, (int)r.Y, (int)r.Width, (int)r.Height);
                }
                return null;
            });
        };

        _viewModel.RequestWindowSelection += (windows) =>
        {
            return Dispatcher.Invoke(() =>
            {
                var dialog = new WindowSelectionDialog(windows);
                if (dialog.ShowDialog() == true)
                    return dialog.SelectedWindow;
                return null;
            });
        };
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
