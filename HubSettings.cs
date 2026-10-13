using System.IO;
using System.ComponentModel;
using System.Text.Json;

namespace GeosangHub;

public sealed class HubSettings
{
    public double Width { get; set; } = 1500;
    public double Height { get; set; } = 950;
    public double? Left { get; set; }
    public double? Top { get; set; }
    public bool WasMaximized { get; set; }
    public double TopPane { get; set; } = 58;
    public double BottomPane { get; set; } = 42;
    public double HelperPane { get; set; } = 34;
    public double MarketPane { get; set; } = 43;
    public double EmptyPane { get; set; } = 23;
    public string BrowserUrl { get; set; } = "https://www.youtube.com/";
    public string HomeUrl { get; set; } = "https://www.youtube.com/";
    public List<string> BrowserTabs { get; set; } = new();
    public int ActiveBrowserTab { get; set; }
    public string SecondaryBrowserUrl { get; set; } = "https://www.youtube.com/";
    public string SecondaryHomeUrl { get; set; } = string.Empty;
    public List<string> SecondaryBrowserTabs { get; set; } = new();
    public int SecondaryActiveBrowserTab { get; set; }
    public double SecondaryBrowserZoom { get; set; } = 1.0;
    public double BrowserZoom { get; set; } = 1.0;
    public double HelperZoom { get; set; } = 1.0;
    public double EmptyZoom { get; set; } = 1.0;
    public decimal JeonRate { get; set; } = 3000m;
    public decimal JeonEokPerTenThousand { get; set; }
    public string MemoText { get; set; } = string.Empty;
    public List<BookmarkItem> Bookmarks { get; set; } = new();

    public static string Folder => Environment.GetEnvironmentVariable("GEOSANG_HUB_DATA")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GeosangIntegratedHub");
    private static string FilePath => Path.Combine(Folder, "layout.json");
    public static string WebViewFolder => Path.Combine(Folder, "WebView2");

    public static HubSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new();
            var settings = JsonSerializer.Deserialize<HubSettings>(File.ReadAllText(FilePath)) ?? new();
            if (settings.JeonEokPerTenThousand <= 0)
                settings.JeonEokPerTenThousand = settings.JeonRate > 0 ? 10_000m / settings.JeonRate : 10_000m / 3000m;
            return settings;
        }
        catch { return new(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed class BookmarkItem : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _url = string.Empty;
    private string _iconUrl = string.Empty;
    public string Name { get => _name; set { if (_name == value) return; _name = value; Changed(nameof(Name)); } }
    public string Url { get => _url; set { if (_url == value) return; _url = value; Changed(nameof(Url)); } }
    public string IconUrl { get => _iconUrl; set { if (_iconUrl == value) return; _iconUrl = value; Changed(nameof(IconUrl)); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
