using System.IO;
using System.Windows;
using Microsoft.Win32;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = System.Windows.Controls.TextBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using MessageBox = System.Windows.MessageBox;

namespace GeosangHelper;

public sealed class ImageShortcutDialog : Window
{
    private readonly TextBox _nameBox;
    private readonly TextBlock _fileText;
    private string _imagePath;

    public string ShortcutName { get; private set; } = "";
    public string ImagePath => _imagePath;
    public bool ImageChanged { get; private set; }

    public ImageShortcutDialog(string name = "", string imagePath = "")
    {
        Title = string.IsNullOrWhiteSpace(name) ? "이미지 버튼 추가" : "이미지 버튼 수정";
        Width = 410;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        _imagePath = imagePath;

        var panel = new StackPanel { Margin = new Thickness(16) };
        Content = panel;
        panel.Children.Add(new TextBlock { Text = "버튼 이름", FontWeight = FontWeights.Bold });
        _nameBox = new TextBox { Text = name, MaxLength = 80, Margin = new Thickness(0, 5, 0, 12) };
        panel.Children.Add(_nameBox);
        panel.Children.Add(new TextBlock { Text = "열어 볼 이미지", FontWeight = FontWeights.Bold });
        _fileText = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(imagePath) ? "선택한 이미지 없음" : Path.GetFileName(imagePath),
            ToolTip = imagePath,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 5, 0, 7)
        };
        panel.Children.Add(_fileText);
        var select = new Button { Content = "이미지 선택", HorizontalAlignment = System.Windows.HorizontalAlignment.Left };
        select.Click += (_, _) => SelectImage();
        panel.Children.Add(select);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };
        buttons.Children.Add(new Button { Content = "취소", IsCancel = true, MinWidth = 65 });
        var save = new Button { Content = "저장", IsDefault = true, MinWidth = 65 };
        save.Click += (_, _) => Save();
        buttons.Children.Add(save);
        panel.Children.Add(buttons);
        Loaded += (_, _) => { _nameBox.Focus(); _nameBox.SelectAll(); };
    }

    private void SelectImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = "버튼에 연결할 이미지 선택",
            Filter = "이미지 파일|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|모든 파일|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true) return;
        _imagePath = dialog.FileName;
        ImageChanged = true;
        _fileText.Text = Path.GetFileName(_imagePath);
        _fileText.ToolTip = _imagePath;
        if (string.IsNullOrWhiteSpace(_nameBox.Text)) _nameBox.Text = Path.GetFileNameWithoutExtension(_imagePath);
    }

    private void Save()
    {
        var name = _nameBox.Text.Trim();
        if (name.Length == 0 || !File.Exists(_imagePath))
        {
            MessageBox.Show(this, "버튼 이름과 이미지 파일을 선택해주세요.", Title);
            return;
        }
        ShortcutName = name;
        DialogResult = true;
    }
}
