using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json.Serialization;
namespace GeosangHelper;
public class MissionTimer : INotifyPropertyChanged
{
    public string Name { get; set; } = "주막 임무";
    // UTC Unix milliseconds, never a decrementing counter.
    public long EndsAtUnixMs { get; set; }
    public long DurationSeconds { get; set; } = 86400;
    public bool IsRunning { get; set; }
    public bool Completed { get; set; }
    public static long RemainingSeconds(long end, long now) => Math.Max(0, (long)Math.Ceiling((end - (double)now) / 1000));
    public static string FormatDuration(long seconds)
    {
        seconds = Math.Max(0, seconds);
        long hours = seconds / 3600;
        long minutes = seconds % 3600 / 60;
        long remainder = seconds % 60;
        return $"{hours:00}시간 {minutes:00}분 {remainder:00}초";
    }
    [JsonIgnore] public string Remaining
    {
        get
        {
            if (Completed) return "✓ 완료 처리됨";
            if (!IsRunning) return FormatDuration(DurationSeconds) + " · 시작 대기";
            long seconds = RemainingSeconds(EndsAtUnixMs, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            if (seconds == 0) return "⏰ 시간 종료 · 완료를 눌러주세요";
            return FormatDuration(seconds);
        }
    }
    [JsonIgnore] public string EndTimeText
    {
        get
        {
            if (EndsAtUnixMs <= 0) return "시작하면 종료 시각이 표시됩니다.";
            var local = DateTimeOffset.FromUnixTimeMilliseconds(EndsAtUnixMs).LocalDateTime;
            return $"{local:yyyy년 MM월 dd일 HH시 mm분 ss초}에 끝납니다.";
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    public void Start(long? nowUnixMs = null)
    {
        long now = nowUnixMs ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        EndsAtUnixMs = checked(now + DurationSeconds * 1000);
        IsRunning = true; Completed = false; Refresh();
    }
    public void Complete() { Completed = true; IsRunning = false; Refresh(); }
    public void Refresh()
    {
        PropertyChanged?.Invoke(this, new(nameof(Remaining)));
        PropertyChanged?.Invoke(this, new(nameof(EndTimeText)));
        PropertyChanged?.Invoke(this, new(nameof(Name)));
    }
}
public class CheckItem : INotifyPropertyChanged
{
    private bool isChecked;
    public string Name { get; set; } = "";
    public bool IsChecked
    {
        get => isChecked;
        set { if (isChecked == value) return; isChecked = value; PropertyChanged?.Invoke(this, new(nameof(IsChecked))); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}
public class BattleRecord
{
    public long StartedAtUnixMs { get; set; }
    public long EndedAtUnixMs { get; set; }
    public string Name { get; set; } = "전투";
    [JsonIgnore] public double DurationSeconds => Math.Max(0, (EndedAtUnixMs - StartedAtUnixMs) / 1000d);
    [JsonIgnore] public string DurationText => $"{DurationSeconds:0.000}초";
    [JsonIgnore] public string TimeText => DateTimeOffset.FromUnixTimeMilliseconds(EndedAtUnixMs).LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
}
public class MarketListing
{
    public long ListingId { get; set; }
    public int ItemId { get; set; }
    public string ItemName { get; set; } = "";
    public int Quantity { get; set; }
    public long UnitPrice { get; set; }
    public string Seller { get; set; } = "";
    public long ObservedAtUnixMs { get; set; }
    [JsonIgnore] public string PriceText => $"{UnitPrice:N0}원";
    [JsonIgnore] public string QuantityText => $"{Quantity:N0}개";
    [JsonIgnore] public string TimeText => DateTimeOffset.FromUnixTimeMilliseconds(ObservedAtUnixMs).LocalDateTime.ToString("HH:mm:ss");
}
public class ImageShortcutItem
{
    public string Name { get; set; } = "";
    public string ImagePath { get; set; } = "";
}
public class AppState
{
    public int Version { get; set; } = 3;
    public double Left { get; set; } = 80;
    public double Top { get; set; } = 80;
    public double Width { get; set; } = 420;
    public double Height { get; set; } = 620;
    public bool AlwaysOnTop { get; set; } = true;
    public bool SizeLocked { get; set; }
    public int SelectedTab { get; set; }
    public string? BackgroundImagePath { get; set; }
    public string FontColor { get; set; } = "#321E18";
    public double FontSize { get; set; } = 14;
    public double PanelOpacity { get; set; } = 0.28;
    public bool GameMonitoringEnabled { get; set; } = true;
    public string BattleLabel { get; set; } = "낭장의혼 · 칠숙의혼";
    public ObservableCollection<MissionTimer> Timers { get; set; } = new();
    public ObservableCollection<CheckItem> Checklist { get; set; } = new();
    public ObservableCollection<BattleRecord> Battles { get; set; } = new();
    public ObservableCollection<MarketListing> MarketListings { get; set; } = new();
    public ObservableCollection<ImageShortcutItem> ImageShortcuts { get; set; } = new();
}
