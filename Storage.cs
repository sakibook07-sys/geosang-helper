using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace GeosangHelper;
public static class Storage
{
    public static string Folder => Environment.GetEnvironmentVariable("GEOSANG_HELPER_DATA")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GeosangHelper");
    public static string FilePath => Path.Combine(Folder, "state.json");
    public static AppState Load()
    {
        if (!File.Exists(FilePath)) return new();
        var state = JsonSerializer.Deserialize<AppState>(File.ReadAllText(FilePath)) ?? throw new InvalidDataException("설정 파일이 비어 있습니다.");
        if (state.Timers == null || state.Checklist == null) throw new InvalidDataException("목록 데이터가 올바르지 않습니다.");
        bool migrated = false;
        if (state.Version == 1)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            foreach (var timer in state.Timers)
            {
                var namedHours = Regex.Match(timer.Name ?? "", @"(?<hours>\d+)\s*시간");
                timer.DurationSeconds = namedHours.Success && long.TryParse(namedHours.Groups["hours"].Value, out long hours) && hours is >= 1 and <= 8760
                    ? hours * 3600
                    : Math.Max(60, MissionTimer.RemainingSeconds(timer.EndsAtUnixMs, now));
                timer.IsRunning = !timer.Completed && timer.EndsAtUnixMs > 0;
            }
            state.Version = 2;
            migrated = true;
        }
        if (state.Version == 2)
        {
            state.Version = 3;
            state.Battles ??= new();
            state.MarketListings ??= new();
            migrated = true;
        }
        if (state.Version == 3)
        {
            state.Version = 4;
            state.ProgramShortcuts ??= new();
            migrated = true;
        }
        state.ImageShortcuts ??= new();
        state.ProgramShortcuts ??= new();
        if (state.Version != 4 || !double.IsFinite(state.Left) || !double.IsFinite(state.Top)
            || !double.IsFinite(state.Width) || !double.IsFinite(state.Height) || state.Width < 1 || state.Height < 1 || state.SelectedTab < 0 || state.SelectedTab > 6
            || !double.IsFinite(state.FontSize) || state.FontSize < 9 || state.FontSize > 32
            || !double.IsFinite(state.PanelOpacity) || state.PanelOpacity < 0 || state.PanelOpacity > 0.9
            || !Regex.IsMatch(state.FontColor ?? "", "^#[0-9A-Fa-f]{6}$")
            || state.Timers.Any(t => t == null || string.IsNullOrWhiteSpace(t.Name) || t.EndsAtUnixMs < 0 || t.EndsAtUnixMs > 253402300799999 || t.DurationSeconds < 60 || t.DurationSeconds > 31536000)
            || state.Checklist.Any(c => c == null || string.IsNullOrWhiteSpace(c.Name))
            || state.Battles == null || state.MarketListings == null || string.IsNullOrWhiteSpace(state.BattleLabel)
            || state.Battles.Any(b => b == null || b.StartedAtUnixMs <= 0 || b.EndedAtUnixMs < b.StartedAtUnixMs)
            || state.MarketListings.Any(m => m == null || m.ListingId <= 0 || m.ItemId <= 0 || m.Quantity <= 0 || m.UnitPrice <= 0)
            || state.ImageShortcuts.Any(i => i == null || string.IsNullOrWhiteSpace(i.Name) || string.IsNullOrWhiteSpace(i.ImagePath))
            || state.ProgramShortcuts.Any(p => p == null || string.IsNullOrWhiteSpace(p.Name) || string.IsNullOrWhiteSpace(p.ExecutablePath)))
            throw new InvalidDataException("설정 파일 형식이 올바르지 않습니다.");
        if (migrated) Save(state);
        return state;
    }
    public static void Save(AppState state)
    {
        Directory.CreateDirectory(Folder);
        string temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak");
        else File.Move(temp, FilePath);
    }
}
