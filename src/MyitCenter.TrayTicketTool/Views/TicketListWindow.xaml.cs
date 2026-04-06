using System.Windows;
using System.Windows.Input;
using MyitCenter.TrayTicketTool.Models;

namespace MyitCenter.TrayTicketTool.Views;

public partial class TicketListWindow : Window
{
    private readonly List<TicketStatusInfo> _tickets;
    private readonly Action<TicketStatusInfo>? _onReply;

    public TicketListWindow(List<TicketStatusInfo> tickets, Action<TicketStatusInfo>? onReply = null)
    {
        InitializeComponent();
        _tickets = tickets;
        _onReply = onReply;

        SubtitleText.Text = $"Angemeldet als: {Environment.UserName}";

        LoadTickets();
    }

    private void LoadTickets()
    {
        if (_tickets.Count == 0)
        {
            EmptyText.Visibility = Visibility.Visible;
            TicketList.Visibility = Visibility.Collapsed;
            ReplyButton.IsEnabled = false;
            return;
        }

        var items = _tickets.Select(t => new TicketListItem
        {
            TicketId = t.TicketId,
            TicketNumber = t.TicketNumber,
            Subject = t.Subject,
            StatusLabel = t.StatusLabel,
            StatusBackground = GetStatusBrush(t.Status),
            ReplyIndicator = t.HasAgentReply ? "Neue Antwort" : "",
            Info = t
        }).ToList();

        TicketList.ItemsSource = items;
        TicketList.SelectionChanged += (_, _) =>
            ReplyButton.IsEnabled = TicketList.SelectedItem != null;

        if (items.Count > 0)
            TicketList.SelectedIndex = 0;
    }

    private static System.Windows.Media.Brush GetStatusBrush(string status)
    {
        return status switch
        {
            "open" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x27, 0xAE, 0x60)),
            "in_progress" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF3, 0x9C, 0x12)),
            "closed" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x95, 0xA5, 0xA6)),
            _ => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x29, 0x80, 0xB9))
        };
    }

    private void Reply_Click(object sender, RoutedEventArgs e)
    {
        if (TicketList.SelectedItem is TicketListItem item)
        {
            _onReply?.Invoke(item.Info);
        }
    }

    private void TicketList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        Reply_Click(sender, e);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    public class TicketListItem
    {
        public int TicketId { get; set; }
        public string TicketNumber { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public System.Windows.Media.Brush StatusBackground { get; set; } = System.Windows.Media.Brushes.Gray;
        public string ReplyIndicator { get; set; } = string.Empty;
        public TicketStatusInfo Info { get; set; } = null!;
    }
}
