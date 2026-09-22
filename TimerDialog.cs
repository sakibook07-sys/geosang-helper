using System.Windows;
using System.Windows.Controls;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;
namespace GeosangHelper;
public class TimerDialog : Window
{
    readonly TextBox name = new() { MaxLength = 80 };
    readonly TextBox hours = new() { Text = "24" };
    readonly TextBox minutes = new() { Text = "0" };
    public string TimerName { get; private set; } = "";
    public long DurationSeconds { get; private set; }
    public TimerDialog(MissionTimer? timer)
    {
        Title = timer == null ? "주막 타이머 추가" : "타이머 수정";
        Width = 340; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        var panel = new StackPanel { Margin = new Thickness(16) }; Content = panel;
        panel.Children.Add(new TextBlock { Text = "임무 이름" }); panel.Children.Add(name); name.Text = timer?.Name ?? "";
        long totalSeconds = Math.Max(60, timer?.DurationSeconds ?? 86400);
        hours.Text = (totalSeconds / 3600).ToString();
        minutes.Text = (totalSeconds % 3600 / 60).ToString();
        panel.Children.Add(new TextBlock { Text = "시간 (0~8760)" }); panel.Children.Add(hours);
        panel.Children.Add(new TextBlock { Text = "분 (0~59)" }); panel.Children.Add(minutes);
        panel.Children.Add(new TextBlock { Text = timer == null ? "저장한 뒤 시작 버튼을 누르면 타이머가 작동합니다." : "진행 중인 타이머는 유지되며, 변경한 시간은 다음 시작부터 적용됩니다.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(3, 10, 3, 10) });
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "취소", IsCancel = true }; row.Children.Add(cancel);
        var save = new Button { Content = "저장", IsDefault = true }; row.Children.Add(save); panel.Children.Add(row);
        save.Click += (_, _) =>
        {
            TimerName = name.Text.Trim();
            if (TimerName.Length == 0) { MessageBox.Show(this, "임무 이름을 입력해주세요."); return; }
            if (!int.TryParse(hours.Text, out int h) || !int.TryParse(minutes.Text, out int m) || h < 0 || h > 8760 || m < 0 || m > 59 || h + m == 0 || h == 8760 && m > 0)
            { MessageBox.Show(this, "시간은 0~8760, 분은 0~59 정수로 입력해주세요. 최소 1분입니다."); return; }
            DurationSeconds = checked((long)h * 3600 + (long)m * 60);
            DialogResult = true;
        };
    }
}
