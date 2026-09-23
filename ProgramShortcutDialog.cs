using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Button = System.Windows.Controls.Button;
using Grid = System.Windows.Controls.Grid;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = System.Windows.Controls.TextBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Orientation = System.Windows.Controls.Orientation;

namespace GeosangHelper;

public sealed class ProgramShortcutDialog : Window
{
    private readonly TextBox _nameBox;
    private readonly TextBox _pathBox;
    private readonly TextBox _argumentsBox;

    public string ShortcutName { get; private set; } = "";
    public string ExecutablePath { get; private set; } = "";
    public string Arguments { get; private set; } = "";

    public ProgramShortcutDialog(string name = "", string executablePath = "", string arguments = "")
    {
        Title = string.IsNullOrWhiteSpace(name) ? "프로그램 버튼 추가" : "프로그램 버튼 수정";
        Width = 520;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var root = new StackPanel { Margin = new Thickness(16) };
        Content = root;
        root.Children.Add(new TextBlock { Text = "버튼 이름", FontWeight = FontWeights.Bold });
        _nameBox = new TextBox { Text = name, MaxLength = 80, Margin = new Thickness(0, 5, 0, 12) };
        root.Children.Add(_nameBox);
        root.Children.Add(new TextBlock { Text = "실행 파일", FontWeight = FontWeights.Bold });
        var pathRow = new Grid { Margin = new Thickness(0, 5, 0, 12) };
        pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _pathBox = new TextBox { Text = executablePath, IsReadOnly = true, VerticalContentAlignment = VerticalAlignment.Center };
        var browse = new Button { Content = "찾아보기", Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(9, 3, 9, 3) };
        browse.Click += (_, _) => ChooseProgram();
        pathRow.Children.Add(_pathBox);
        Grid.SetColumn(browse, 1);
        pathRow.Children.Add(browse);
        root.Children.Add(pathRow);
        root.Children.Add(new TextBlock { Text = "실행 옵션 · 비워도 됩니다", FontWeight = FontWeights.Bold });
        _argumentsBox = new TextBox { Text = arguments, Margin = new Thickness(0, 5, 0, 0) };
        root.Children.Add(_argumentsBox);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        buttons.Children.Add(new Button { Content = "취소", IsCancel = true, MinWidth = 70 });
        var save = new Button { Content = "저장", IsDefault = true, MinWidth = 70, Margin = new Thickness(5, 0, 0, 0) };
        save.Click += (_, _) => Save();
        buttons.Children.Add(save);
        root.Children.Add(buttons);
        Loaded += (_, _) => _nameBox.Focus();
    }

    private void ChooseProgram()
    {
        var dialog = new OpenFileDialog
        {
            Title = "상단에서 실행할 프로그램 선택",
            Filter = "프로그램 (*.exe)|*.exe|모든 파일|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true) return;
        _pathBox.Text = dialog.FileName;
        if (string.IsNullOrWhiteSpace(_nameBox.Text)) _nameBox.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
    }

    private void Save()
    {
        string name = _nameBox.Text.Trim();
        string path = _pathBox.Text.Trim();
        if (name.Length == 0 || !File.Exists(path))
        {
            MessageBox.Show(this, "버튼 이름과 실행 파일을 선택해주세요.", Title);
            return;
        }
        ShortcutName = name;
        ExecutablePath = path;
        Arguments = _argumentsBox.Text.Trim();
        DialogResult = true;
    }
}
