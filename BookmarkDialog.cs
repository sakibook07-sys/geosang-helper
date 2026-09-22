using System.Windows;
using System.Windows.Controls;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBox = System.Windows.Controls.TextBox;
using TextBlock = System.Windows.Controls.TextBlock;

namespace GeosangHub;

public sealed class BookmarkDialog : Window
{
    private readonly TextBox _nameBox;
    public string BookmarkName => _nameBox.Text.Trim();

    public BookmarkDialog(string suggestedName, string url, string dialogTitle = "북마크 추가", string confirmText = "추가")
    {
        Title = dialogTitle;
        Width = 430;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var panel = new StackPanel { Margin = new Thickness(18) };
        Content = panel;
        panel.Children.Add(new TextBlock { Text = "북마크 이름", FontWeight = FontWeights.Bold });
        _nameBox = new TextBox { Text = suggestedName, MaxLength = 100, Margin = new Thickness(0, 6, 0, 10) };
        panel.Children.Add(_nameBox);
        panel.Children.Add(new TextBlock { Text = url, TextWrapping = TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.DimGray });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        buttons.Children.Add(new Button { Content = "취소", IsCancel = true });
        var save = new Button { Content = confirmText, IsDefault = true };
        save.Click += (_, _) =>
        {
            if (BookmarkName.Length == 0) return;
            DialogResult = true;
        };
        buttons.Children.Add(save);
        panel.Children.Add(buttons);

        Loaded += (_, _) => { _nameBox.SelectAll(); _nameBox.Focus(); };
    }
}
