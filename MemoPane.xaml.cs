using System.Windows;
using System.Windows.Controls;
using MessageBox = System.Windows.MessageBox;
using UserControl = System.Windows.Controls.UserControl;
using WpfClipboard = System.Windows.Clipboard;

namespace GeosangHub;

public partial class MemoPane : UserControl
{
    private bool _loading;
    public event EventHandler<string>? MemoChanged;

    public MemoPane() => InitializeComponent();

    public void SetText(string? text)
    {
        _loading = true;
        MemoTextBox.Text = text ?? string.Empty;
        _loading = false;
        SaveStatusText.Text = MemoTextBox.Text.Length == 0 ? "입력을 멈추면 자동 저장됩니다." : "저장된 메모를 불러왔습니다.";
    }

    public void MarkSaved(DateTime savedAt) => SaveStatusText.Text = $"저장됨 · {savedAt:HH:mm:ss}";

    public void MarkSaveFailed(string message)
    {
        SaveStatusText.Text = "저장 실패";
        SaveStatusText.ToolTip = message;
    }

    private void MemoTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading) return;
        SaveStatusText.Text = "저장 중…";
        MemoChanged?.Invoke(this, MemoTextBox.Text);
    }

    private void CopyAll_Click(object sender, RoutedEventArgs e)
    {
        if (MemoTextBox.Text.Length == 0) return;
        try
        {
            WpfClipboard.SetText(MemoTextBox.Text);
            SaveStatusText.Text = "전체 메모를 복사했습니다.";
        }
        catch (Exception ex) { MessageBox.Show(Window.GetWindow(this), "메모를 복사하지 못했습니다.\n" + ex.Message); }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        MemoTextBox.Clear();
        MemoTextBox.Focus();
    }
}
