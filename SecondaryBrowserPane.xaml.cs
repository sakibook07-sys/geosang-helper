using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using UserControl = System.Windows.Controls.UserControl;

namespace GeosangHub;

public partial class SecondaryBrowserPane : UserControl, IDisposable
{
    private readonly ObservableCollection<TabEntry> _tabs = new();
    private ObservableCollection<BookmarkItem>? _bookmarks;
    private Action? _saveBookmarks;
    private CoreWebView2Environment? _environment;
    private TabEntry? _activeTab;
    private string _homeUrl = "https://www.youtube.com/";
    private double _zoom = 1.0;
    private bool _isClosing;

    public SecondaryBrowserPane()
    {
        InitializeComponent();
        TabItems.ItemsSource = _tabs;
    }

    public async Task InitializeAsync(CoreWebView2Environment environment, HubSettings settings,
        ObservableCollection<BookmarkItem> bookmarks, Action saveBookmarks)
    {
        _environment = environment;
        _bookmarks = bookmarks;
        _saveBookmarks = saveBookmarks;
        BookmarkItems.ItemsSource = bookmarks;
        _homeUrl = NormalizeHome(settings.SecondaryHomeUrl, settings.HomeUrl);
        _zoom = NormalizeZoom(settings.SecondaryBrowserZoom);
        ZoomText.Text = $"{_zoom:P0}";
        StatusText.Text = "브라우저 준비 중";

        var savedTabs = (settings.SecondaryBrowserTabs ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x)).Take(12).ToList();
        if (savedTabs.Count == 0) savedTabs.Add(
            string.IsNullOrWhiteSpace(settings.SecondaryBrowserUrl) ? _homeUrl : settings.SecondaryBrowserUrl);

        foreach (string url in savedTabs) await CreateTabAsync(url, select: false);
        if (_tabs.Count == 0) await CreateTabAsync(_homeUrl, select: true);
        else SelectTab(_tabs[Math.Clamp(settings.SecondaryActiveBrowserTab, 0, _tabs.Count - 1)]);
        StatusText.Text = "준비 완료";
    }

    public void SaveState(HubSettings settings)
    {
        settings.SecondaryHomeUrl = _homeUrl;
        settings.SecondaryBrowserZoom = _zoom;
        settings.SecondaryBrowserUrl = CurrentView?.Source?.AbsoluteUri ?? AddressBox.Text;
        settings.SecondaryBrowserTabs = _tabs.Select(x => x.Url)
            .Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        settings.SecondaryActiveBrowserTab = Math.Max(0, _activeTab == null ? 0 : _tabs.IndexOf(_activeTab));
    }

    private async Task<TabEntry?> CreateTabAsync(string input, bool select)
    {
        if (_environment == null || _isClosing) return null;
        string destination = NormalizeInput(input);
        string title;
        try { title = new Uri(destination).Host; } catch { title = "새 탭"; }

        var view = new WebView2
        {
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
            VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
            Visibility = Visibility.Visible,
            ZoomFactor = _zoom
        };
        var tab = new TabEntry(view) { Title = title, Url = destination };
        _tabs.Add(tab);
        ViewsHost.Children.Add(view);

        try
        {
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            await view.EnsureCoreWebView2Async(_environment);
            ConfigureTab(tab);
            view.CoreWebView2.Navigate(destination);
            if (select) SelectTab(tab); else view.Visibility = Visibility.Collapsed;
            return tab;
        }
        catch
        {
            ViewsHost.Children.Remove(view);
            _tabs.Remove(tab);
            view.Dispose();
            StatusText.Text = "탭을 열지 못했습니다";
            return null;
        }
    }

    private void ConfigureTab(TabEntry tab)
    {
        var view = tab.View;
        view.CoreWebView2.Settings.IsStatusBarEnabled = false;
        view.CoreWebView2.Settings.AreDevToolsEnabled = false;
        view.CoreWebView2.NavigationStarting += (_, args) =>
        {
            tab.Url = args.Uri;
            if (_activeTab == tab) AddressBox.Text = args.Uri;
            StatusText.Text = "불러오는 중";
        };
        view.CoreWebView2.NavigationCompleted += (_, args) =>
            StatusText.Text = args.IsSuccess ? "불러오기 완료" : "페이지를 불러오지 못했습니다";
        view.CoreWebView2.DocumentTitleChanged += (_, _) =>
        {
            string title = view.CoreWebView2.DocumentTitle;
            if (!string.IsNullOrWhiteSpace(title)) tab.Title = title;
        };
        view.CoreWebView2.NewWindowRequested += (_, args) =>
        {
            args.Handled = true;
            if (!_isClosing) _ = CreateTabAsync(args.Uri, select: true);
        };
    }

    private WebView2? CurrentView => _activeTab?.View;

    private void SelectTab(TabEntry tab)
    {
        foreach (var item in _tabs)
        {
            bool active = item == tab;
            item.IsActive = active;
            item.View.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        }
        _activeTab = tab;
        AddressBox.Text = tab.Url;
        StatusText.Text = tab.Title;
    }

    private void Navigate(string? input)
    {
        if (CurrentView?.CoreWebView2 == null) return;
        string destination = NormalizeInput(input);
        AddressBox.Text = destination;
        CurrentView.CoreWebView2.Navigate(destination);
    }

    private string NormalizeInput(string? input)
    {
        string value = input?.Trim() ?? string.Empty;
        if (value.Length == 0) value = _homeUrl;
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            return uri.AbsoluteUri;
        if (value.Contains('.') && !value.Contains(' ')) return "https://" + value;
        return "https://search.naver.com/search.naver?query=" + Uri.EscapeDataString(value);
    }

    private static string NormalizeHome(string? value, string fallback)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https") return uri.AbsoluteUri;
        if (Uri.TryCreate(fallback, UriKind.Absolute, out uri) && uri.Scheme is "http" or "https") return uri.AbsoluteUri;
        return "https://www.youtube.com/";
    }

    private static double NormalizeZoom(double value) =>
        double.IsFinite(value) ? Math.Clamp(Math.Round(value, 1), 0.5, 2.0) : 1.0;

    private void SetZoom(double value)
    {
        _zoom = NormalizeZoom(value);
        foreach (var tab in _tabs) tab.View.ZoomFactor = _zoom;
        ZoomText.Text = $"{_zoom:P0}";
    }

    private void Go_Click(object sender, RoutedEventArgs e) => Navigate(AddressBox.Text);
    private void AddressBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { Navigate(AddressBox.Text); e.Handled = true; }
    }
    private void Back_Click(object sender, RoutedEventArgs e) { if (CurrentView is { CanGoBack: true } view) view.GoBack(); }
    private void Forward_Click(object sender, RoutedEventArgs e) { if (CurrentView is { CanGoForward: true } view) view.GoForward(); }
    private void Home_Click(object sender, RoutedEventArgs e) => Navigate(_homeUrl);
    private void Refresh_Click(object sender, RoutedEventArgs e) { if (CurrentView?.CoreWebView2 != null) CurrentView.Reload(); }
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => SetZoom(_zoom - 0.1);
    private void ZoomIn_Click(object sender, RoutedEventArgs e) => SetZoom(_zoom + 0.1);
    private async void NewTab_Click(object sender, RoutedEventArgs e) => await CreateTabAsync(_homeUrl, select: true);

    private void HomeSettings_Click(object sender, RoutedEventArgs e)
    {
        string? currentUrl = CurrentView?.Source?.AbsoluteUri ?? _activeTab?.Url;
        var dialog = new HomeAddressDialog(_homeUrl, currentUrl) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true) _homeUrl = dialog.HomeUrl;
    }

    private void Tab_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is TabEntry tab) SelectTab(tab);
    }

    private async void TabClose_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not TabEntry tab) return;
        int oldIndex = _tabs.IndexOf(tab);
        bool wasActive = tab == _activeTab;
        ViewsHost.Children.Remove(tab.View);
        _tabs.Remove(tab);
        tab.View.Dispose();
        if (!wasActive) return;
        _activeTab = null;
        if (_tabs.Count == 0) await CreateTabAsync(_homeUrl, select: true);
        else SelectTab(_tabs[Math.Clamp(oldIndex, 0, _tabs.Count - 1)]);
    }

    private void BookmarkAdd_Click(object sender, RoutedEventArgs e)
    {
        if (_bookmarks == null || _activeTab == null) return;
        string url = CurrentView?.Source?.AbsoluteUri ?? _activeTab.Url;
        string title = string.IsNullOrWhiteSpace(_activeTab.Title) ? url : _activeTab.Title;
        var dialog = new BookmarkDialog(title, url) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true) return;
        var existing = _bookmarks.FirstOrDefault(x => string.Equals(x.Url, url, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            existing.Name = dialog.BookmarkName;
            BookmarkItems.Items.Refresh();
        }
        else
        {
            _bookmarks.Add(new BookmarkItem { Name = dialog.BookmarkName, Url = url, IconUrl = GetFavicon(url) });
        }
        _saveBookmarks?.Invoke();
    }

    private void Bookmark_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is BookmarkItem item) Navigate(item.Url);
    }

    private static BookmarkItem? BookmarkFromMenu(object sender) =>
        (sender as FrameworkElement)?.DataContext as BookmarkItem;

    private void BookmarkContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu && menu.PlacementTarget is FrameworkElement target)
            menu.DataContext = target.DataContext;
    }

    private void BookmarkRename_Click(object sender, RoutedEventArgs e)
    {
        var item = BookmarkFromMenu(sender);
        if (item == null) return;
        var dialog = new BookmarkDialog(item.Name, item.Url, "북마크 이름 변경", "저장") { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true) return;
        item.Name = dialog.BookmarkName;
        BookmarkItems.Items.Refresh();
        _saveBookmarks?.Invoke();
    }

    private void BookmarkMoveLeft_Click(object sender, RoutedEventArgs e)
    {
        var item = BookmarkFromMenu(sender);
        if (item == null || _bookmarks == null) return;
        int index = _bookmarks.IndexOf(item);
        if (index > 0) _bookmarks.Move(index, index - 1);
        _saveBookmarks?.Invoke();
    }

    private void BookmarkMoveRight_Click(object sender, RoutedEventArgs e)
    {
        var item = BookmarkFromMenu(sender);
        if (item == null || _bookmarks == null) return;
        int index = _bookmarks.IndexOf(item);
        if (index >= 0 && index < _bookmarks.Count - 1) _bookmarks.Move(index, index + 1);
        _saveBookmarks?.Invoke();
    }

    private void BookmarkDelete_Click(object sender, RoutedEventArgs e)
    {
        var item = BookmarkFromMenu(sender);
        if (item == null || _bookmarks == null) return;
        _bookmarks.Remove(item);
        _saveBookmarks?.Invoke();
    }

    private string GetFavicon(string pageUrl)
    {
        try
        {
            string favicon = CurrentView?.CoreWebView2?.FaviconUri ?? string.Empty;
            if (Uri.TryCreate(favicon, UriKind.Absolute, out _)) return favicon;
        }
        catch { }
        try { return new Uri(new Uri(pageUrl), "/favicon.ico").AbsoluteUri; }
        catch { return string.Empty; }
    }

    public void Dispose()
    {
        _isClosing = true;
        foreach (var tab in _tabs) tab.View.Dispose();
        _tabs.Clear();
    }

    private sealed class TabEntry : INotifyPropertyChanged
    {
        private string _title = "새 탭";
        private string _url = string.Empty;
        private bool _isActive;
        public TabEntry(WebView2 view) => View = view;
        public WebView2 View { get; }
        public string Title { get => _title; set { if (_title == value) return; _title = value; Changed(nameof(Title)); } }
        public string Url { get => _url; set { if (_url == value) return; _url = value; Changed(nameof(Url)); } }
        public bool IsActive { get => _isActive; set { if (_isActive == value) return; _isActive = value; Changed(nameof(IsActive)); } }
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
