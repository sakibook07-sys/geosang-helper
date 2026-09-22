using System.IO;
using System.Text.Json;
using GeotaMarketViewer;

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
    public int ServerId { get; set; } = 3;
    public string MarketSearch { get; set; } = string.Empty;
    public MarketSort MarketSort { get; set; } = MarketSort.Latest;
    public int MarketPage { get; set; } = 1;
    public double BrowserZoom { get; set; } = 1.0;
    public double HelperZoom { get; set; } = 1.0;
    public double MarketZoom { get; set; } = 1.0;
    public double EmptyZoom { get; set; } = 1.0;
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
            return JsonSerializer.Deserialize<HubSettings>(File.ReadAllText(FilePath)) ?? new();
        }
        catch { return new(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed class BookmarkItem
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
}
