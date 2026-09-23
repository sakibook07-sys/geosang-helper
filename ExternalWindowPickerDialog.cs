using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Button = System.Windows.Controls.Button;
using Grid = System.Windows.Controls.Grid;
using ListBox = System.Windows.Controls.ListBox;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MessageBox = System.Windows.MessageBox;
using Orientation = System.Windows.Controls.Orientation;

namespace GeosangHub;

public sealed class ExternalWindowPickerDialog : Window
{
    private readonly ListBox _windows;
    public ExternalWindowInfo? SelectedWindow => _windows.SelectedItem as ExternalWindowInfo;

    public ExternalWindowPickerDialog(IReadOnlyList<ExternalWindowInfo> windows)
    {
        Title = "상단에 넣을 프로그램 선택";
        Width = 620;
        Height = 430;
        MinWidth = 440;
        MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;

        var root = new Grid { Margin = new Thickness(14) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(new TextBlock
        {
            Text = "실행 중인 프로그램을 선택하세요. 연결하면 창이 상단 영역 크기에 맞춰집니다.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10)
        });

        _windows = new ListBox { ItemsSource = windows, DisplayMemberPath = nameof(ExternalWindowInfo.DisplayName) };
        _windows.MouseDoubleClick += (_, _) => Accept();
        Grid.SetRow(_windows, 1);
        root.Children.Add(_windows);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        var refreshHint = new TextBlock { Text = "목록에 없다면 프로그램을 먼저 실행하세요.", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0), Foreground = System.Windows.Media.Brushes.DimGray };
        var cancel = new Button { Content = "취소", Width = 80, Margin = new Thickness(4, 0, 0, 0), IsCancel = true };
        var ok = new Button { Content = "연결", Width = 80, Margin = new Thickness(4, 0, 0, 0), IsDefault = true };
        ok.Click += (_, _) => Accept();
        buttons.Children.Add(refreshHint);
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        Grid.SetRow(buttons, 2);
        root.Children.Add(buttons);
        Content = root;
        if (windows.Count > 0) _windows.SelectedIndex = 0;
    }

    private void Accept()
    {
        if (SelectedWindow == null)
        {
            MessageBox.Show(this, "연결할 프로그램을 선택해주세요.", "프로그램 연결", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }
}
