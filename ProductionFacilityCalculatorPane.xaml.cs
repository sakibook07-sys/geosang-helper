using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using UserControl = System.Windows.Controls.UserControl;

namespace GeosangHub;

public partial class ProductionFacilityCalculatorPane : UserControl
{
    private static readonly WageTier[] WageTiers =
    [
        new(100, 20), new(200, 30), new(300, 50), new(400, 80), new(500, 120),
        new(600, 170), new(700, 230), new(800, 300), new(900, 380), new(1000, 500),
        new(1100, 600), new(1200, 700), new(1300, 800), new(1400, 900), new(1500, 1000),
        new(1600, 1100), new(1700, 1200), new(1800, 1300), new(1900, 1400), new(2000, 1500),
        new(2100, 1600), new(2200, 1700), new(2300, 1800), new(2400, 1900), new(2500, 2000),
        new(2600, 2100), new(2700, 2200), new(2800, 2300), new(2900, 2400), new(3000, 2500, true)
    ];

    private bool _ready;
    private long _durationSeconds;
    private string _timerName = "생산시설";
    public event EventHandler<ProductionTimerEventArgs>? TimerStartRequested;

    public ProductionFacilityCalculatorPane()
    {
        InitializeComponent();
        WageCombo.ItemsSource = WageTiers;
        WageCombo.SelectedIndex = 9;
        _ready = true;
        Recalculate();
    }

    private void InputChanged(object sender, EventArgs e)
    {
        if (_ready) Recalculate();
    }

    private void Recalculate()
    {
        _durationSeconds = 0;
        StartTimerButton.IsEnabled = false;
        StatusText.Text = string.Empty;
        if (WageCombo.SelectedItem is not WageTier tier || !long.TryParse(WorkloadInput.Text.Replace(",", "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long workload) || workload <= 0)
        {
            CapacityText.Text = "게임 하루 작업량: -";
            GameTimeText.Text = RealTimeText.Text = "-";
            StatusText.Text = "작업량을 0보다 큰 정수로 입력해주세요.";
            return;
        }

        decimal gameDays = workload / (decimal)tier.GameWorkPerDay;
        decimal realSeconds = gameDays * 48m * 60m;
        if (realSeconds > 31_536_000m)
        {
            StatusText.Text = "계산 결과가 1년을 넘어서 타이머로 만들 수 없습니다.";
            return;
        }

        _durationSeconds = Math.Max(1, (long)decimal.Ceiling(realSeconds));
        long gameSeconds = Math.Max(1, (long)decimal.Ceiling(gameDays * 86_400m));
        CapacityText.Text = $"게임 하루 {tier.GameWorkPerDay:N0} · 현실 하루 {tier.GameWorkPerDay * 30:N0} 작업";
        GameTimeText.Text = FormatDuration(gameSeconds);
        RealTimeText.Text = FormatDuration(_durationSeconds);
        _timerName = $"생산시설 · 임금 {tier.Wage:N0} · 작업량 {workload:N0}";
        StartTimerButton.IsEnabled = true;
    }

    private void StartTimer_Click(object sender, RoutedEventArgs e)
    {
        if (_durationSeconds <= 0) return;
        if (TimerStartRequested == null)
        {
            StatusText.Text = "타이머 화면에 연결하지 못했습니다.";
            return;
        }
        var request = new ProductionTimerEventArgs(_timerName, _durationSeconds);
        TimerStartRequested.Invoke(this, request);
        StatusText.Text = request.Added ? "타이머에 추가하고 바로 시작했습니다." : "타이머를 저장하지 못했습니다.";
    }

    private static string FormatDuration(long totalSeconds)
    {
        var span = TimeSpan.FromSeconds(totalSeconds);
        var parts = new List<string>();
        if (span.Days > 0) parts.Add($"{span.Days}일");
        if (span.Hours > 0 || parts.Count > 0) parts.Add($"{span.Hours:00}시간");
        if (span.Minutes > 0 || parts.Count > 0) parts.Add($"{span.Minutes:00}분");
        parts.Add($"{span.Seconds:00}초");
        return string.Join(" ", parts);
    }

    private sealed record WageTier(int Wage, int GameWorkPerDay, bool IsMaximum = false)
    {
        public string Name => IsMaximum ? $"{Wage:N0} 이상" : $"{Wage:N0}";
    }
}

public sealed record ProductionTimerEventArgs(string Name, long DurationSeconds)
{
    public bool Added { get; set; }
}
