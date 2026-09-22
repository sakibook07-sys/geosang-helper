using System.Windows;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = System.Windows.Controls.TextBox;

namespace GeosangHub;

public sealed class HomeAddressDialog : Window
{
    private readonly TextBox _addressBox;
    public string HomeUrl { get; private set; } = string.Empty;

    public HomeAddressDialog(string savedUrl, string? currentUrl)
    {
        Title = "홈 주소 설정";
        Width = 500;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var panel = new StackPanel { Margin = new Thickness(18) };
        Content = panel;
        panel.Children.Add(new TextBlock
        {
            Text = "홈 버튼과 새 탭에서 열 주소를 입력하세요.",
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 7)
        });
        _addressBox = new TextBox { Text = savedUrl, Margin = new Thickness(0, 0, 0, 8) };
        panel.Children.Add(_addressBox);

        if (!string.IsNullOrWhiteSpace(currentUrl))
        {
            var useCurrent = new Button { Content = "현재 열린 페이지 주소 사용", HorizontalAlignment = System.Windows.HorizontalAlignment.Left };
            useCurrent.Click += (_, _) => _addressBox.Text = currentUrl;
            panel.Children.Add(useCurrent);
        }

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        buttons.Children.Add(new Button { Content = "취소", IsCancel = true });
        var save = new Button { Content = "저장", IsDefault = true };
        save.Click += (_, _) => Save();
        buttons.Children.Add(save);
        panel.Children.Add(buttons);
        Loaded += (_, _) => { _addressBox.SelectAll(); _addressBox.Focus(); };
    }

    private void Save()
    {
        string value = _addressBox.Text.Trim();
        if (!value.Contains("://", StringComparison.Ordinal)) value = "https://" + value;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            System.Windows.MessageBox.Show(this, "http 또는 https로 시작하는 올바른 주소를 입력해주세요.", "홈 주소 설정");
            return;
        }
        HomeUrl = uri.AbsoluteUri;
        DialogResult = true;
    }
}
