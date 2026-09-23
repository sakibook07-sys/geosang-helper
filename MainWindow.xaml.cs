using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;
using GeotaMarketViewer;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Windows.Threading;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;

namespace GeosangHub;

public partial class MainWindow : Window
{
    private readonly HubSettings _settings = HubSettings.Load();
    private readonly ObservableCollection<BookmarkItem> _bookmarks;
    private readonly ObservableCollection<BrowserTabEntry> _browserTabs = new();
    private BrowserTabEntry? _activeBrowserTab;
    private CoreWebView2Environment? _webEnvironment;
    private bool _browserReady;
    private bool _marketReady;
    private bool _isClosing;
    private readonly ExternalWindowDock _externalWindowDock;

    private static readonly ServerOption[] Servers =
    {
        new(1, "백호"), new(2, "주작"), new(3, "현무"), new(4, "청룡"),
        new(5, "봉황"), new(6, "해태"), new(7, "세종"), new(8, "신구"),
        new(9, "단군"), new(10, "비호"), new(11, "태극"), new(12, "화랑"),
        new(13, "태왕")
    };

    public MainWindow()
    {
        InitializeComponent();
        _externalWindowDock = new ExternalWindowDock(ExternalProgramPanel);
        _externalWindowDock.Detached += (_, _) => Dispatcher.Invoke(ShowBrowserArea);
        _bookmarks = new ObservableCollection<BookmarkItem>(_settings.Bookmarks ?? new List<BookmarkItem>());
        foreach (var bookmark in _bookmarks) EnsureBookmarkIcon(bookmark);
        BookmarkItems.ItemsSource = _bookmarks;
        BrowserTabItems.ItemsSource = _browserTabs;
        ImageShortcutButtons.ItemsSource = HelperPane.ImageShortcuts;
        ProgramShortcutButtons.ItemsSource = HelperPane.ProgramShortcuts;
        ApplySettings();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private void ApplySettings()
    {
        Width = Math.Max(MinWidth, _settings.Width);
        Height = Math.Max(MinHeight, _settings.Height);
        if (_settings.Left is double left && _settings.Top is double top)
        {
            var work = SystemParameters.VirtualScreenWidth;
            if (left < SystemParameters.VirtualScreenLeft + work && top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight)
            {
                Left = left;
                Top = top;
                WindowStartupLocation = WindowStartupLocation.Manual;
            }
        }

        TopRow.Height = new GridLength(Math.Max(10, _settings.TopPane), GridUnitType.Star);
        BottomRow.Height = new GridLength(Math.Max(10, _settings.BottomPane), GridUnitType.Star);
        HelperColumn.Width = new GridLength(Math.Max(10, _settings.HelperPane), GridUnitType.Star);
        MarketColumn.Width = new GridLength(Math.Max(10, _settings.MarketPane), GridUnitType.Star);
        EmptyColumn.Width = new GridLength(Math.Max(10, _settings.EmptyPane), GridUnitType.Star);

        BrowserAddress.Text = _settings.BrowserUrl;
        if (!Uri.TryCreate(_settings.HomeUrl, UriKind.Absolute, out var savedHome) || savedHome.Scheme is not ("http" or "https"))
            _settings.HomeUrl = "https://www.youtube.com/";
        ServerCombo.ItemsSource = Servers;
        ServerCombo.SelectedValue = Math.Clamp(_settings.ServerId, 1, 13);
        MarketSearchBox.Text = _settings.MarketSearch;
        MarketSortCombo.SelectedIndex = Math.Clamp((int)_settings.MarketSort, 0, 2);
        MarketPageText.Text = $"{Math.Max(1, _settings.MarketPage)} 페이지";
        _settings.BrowserZoom = NormalizeZoom(_settings.BrowserZoom);
        _settings.HelperZoom = NormalizeZoom(_settings.HelperZoom);
        _settings.MarketZoom = NormalizeZoom(_settings.MarketZoom);
        _settings.EmptyZoom = NormalizeZoom(_settings.EmptyZoom);
        UpdateZoomDisplays();
        ApplyHelperZoom();
        ApplyEmptyZoom();
        if (_settings.WasMaximized) WindowState = WindowState.Maximized;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            MarketStatus.Text = "브라우저 준비 중";
            _webEnvironment = await CoreWebView2Environment.CreateAsync(userDataFolder: HubSettings.WebViewFolder);
        }
        catch (Exception ex)
        {
            MarketStatus.Text = "브라우저 환경 시작 실패";
            ShowStartupError("내장 브라우저 환경을 준비하지 못했습니다.", ex);
            return;
        }

        try
        {
            _browserReady = true;
            var savedTabs = (_settings.BrowserTabs ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x)).Take(12).ToList();
            if (savedTabs.Count == 0) savedTabs.Add(_settings.BrowserUrl);
            foreach (string url in savedTabs) await CreateBrowserTabAsync(url, select: false);
            if (_browserTabs.Count == 0)
                throw new InvalidOperationException("인터넷 탭을 초기화하지 못했습니다.");
            SelectBrowserTab(_browserTabs[Math.Clamp(_settings.ActiveBrowserTab, 0, _browserTabs.Count - 1)]);
        }
        catch (Exception ex)
        {
            BrowserAddress.Text = "인터넷 화면 시작 실패";
            WriteErrorLog("Browser initialization", ex);
        }

        try
        {
            await MarketView.EnsureCoreWebView2Async(_webEnvironment);
            ConfigureMarket();
            _marketReady = true;
            MarketView.ZoomFactor = _settings.MarketZoom;
            NavigateMarket(resetPage: false);
        }
        catch (Exception ex)
        {
            MarketStatus.Text = "육의전 화면 시작 실패 · 새로고침을 눌러주세요";
            WriteErrorLog("Market initialization", ex);
        }
    }

    private async Task<BrowserTabEntry?> CreateBrowserTabAsync(string url, bool select)
    {
        if (_webEnvironment == null || _isClosing) return null;
        string destination = NormalizeBrowserInput(url);
        string title;
        try { title = new Uri(destination).Host; }
        catch { title = "새 탭"; }
        var view = new WebView2
        {
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
            VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
            Visibility = Visibility.Visible,
            ZoomFactor = _settings.BrowserZoom
        };
        var tab = new BrowserTabEntry { Title = title, Url = destination, View = view };
        _browserTabs.Add(tab);
        BrowserViewsHost.Children.Add(view);

        try
        {
            // WebView2 needs a real WPF host handle. Let layout/render finish after adding it.
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            await view.EnsureCoreWebView2Async(_webEnvironment);
            ConfigureBrowser(tab);
            view.CoreWebView2.Navigate(destination);
            if (select) SelectBrowserTab(tab);
            else view.Visibility = Visibility.Collapsed;
            return tab;
        }
        catch (Exception ex)
        {
            WriteErrorLog("Browser tab initialization", ex);
            BrowserViewsHost.Children.Remove(view);
            _browserTabs.Remove(tab);
            view.Dispose();
            return null;
        }
    }

    private void ConfigureBrowser(BrowserTabEntry tab)
    {
        var view = tab.View;
        view.CoreWebView2.Settings.IsStatusBarEnabled = false;
        view.CoreWebView2.Settings.AreDevToolsEnabled = false;
        view.CoreWebView2.NavigationStarting += (_, args) =>
        {
            tab.Url = args.Uri;
            if (_activeBrowserTab == tab) BrowserAddress.Text = args.Uri;
        };
        view.CoreWebView2.DocumentTitleChanged += (_, _) =>
        {
            string title = view.CoreWebView2.DocumentTitle;
            tab.Title = string.IsNullOrWhiteSpace(title) ? tab.Title : title;
        };
        view.CoreWebView2.NewWindowRequested += (_, args) =>
        {
            args.Handled = true;
            if (!_isClosing) _ = OpenNewBrowserTabSafeAsync(args.Uri);
        };
    }

    private async Task OpenNewBrowserTabSafeAsync(string url)
    {
        try { await CreateBrowserTabAsync(url, select: true); }
        catch (Exception ex) { WriteErrorLog("Open new browser tab", ex); }
    }

    private void ConfigureMarket()
    {
        MarketView.CoreWebView2.Settings.IsStatusBarEnabled = false;
        MarketView.CoreWebView2.Settings.AreDevToolsEnabled = false;
        MarketView.CoreWebView2.NavigationStarting += (_, _) => MarketStatus.Text = "불러오는 중";
        MarketView.CoreWebView2.NavigationCompleted += (_, args) =>
            MarketStatus.Text = args.IsSuccess ? "불러오기 완료" : "불러오기 실패";
        MarketView.CoreWebView2.NewWindowRequested += (_, args) =>
        {
            args.Handled = true;
            MarketView.CoreWebView2.Navigate(args.Uri);
        };
    }

    private void NavigateBrowser(string? input)
    {
        var browser = CurrentBrowser;
        if (!_browserReady || browser?.CoreWebView2 == null) return;
        string destination = NormalizeBrowserInput(input);
        _settings.BrowserUrl = destination;
        BrowserAddress.Text = destination;
        browser.CoreWebView2.Navigate(destination);
    }

    private string NormalizeBrowserInput(string? input)
    {
        var value = input?.Trim() ?? string.Empty;
        if (value.Length == 0) value = _settings.HomeUrl;

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            return uri.AbsoluteUri;
        else if (value.Contains('.') && !value.Contains(' '))
            return "https://" + value;
        return "https://search.naver.com/search.naver?query=" + Uri.EscapeDataString(value);
    }

    private void NavigateMarket(bool resetPage)
    {
        if (!_marketReady) return;
        if (resetPage) _settings.MarketPage = 1;
        _settings.MarketPage = Math.Max(1, _settings.MarketPage);
        _settings.ServerId = ServerCombo.SelectedValue is int id ? id : 3;
        _settings.MarketSearch = MarketSearchBox.Text.Trim();
        _settings.MarketSort = SelectedMarketSort();
        MarketPageText.Text = $"{_settings.MarketPage} 페이지";
        MarketView.CoreWebView2.Navigate(MarketUrlBuilder.Build(
            _settings.ServerId, _settings.MarketSearch, _settings.MarketSort, _settings.MarketPage).AbsoluteUri);
    }

    private MarketSort SelectedMarketSort()
    {
        if (MarketSortCombo.SelectedItem is ComboBoxItem item &&
            Enum.TryParse<MarketSort>(item.Tag?.ToString(), out var sort)) return sort;
        return MarketSort.Latest;
    }

    private void BrowserGo_Click(object sender, RoutedEventArgs e) => NavigateBrowser(BrowserAddress.Text);
    private void BrowserAddress_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { NavigateBrowser(BrowserAddress.Text); e.Handled = true; }
    }
    private WebView2? CurrentBrowser => _activeBrowserTab?.View;
    private void BrowserBack_Click(object sender, RoutedEventArgs e) { if (CurrentBrowser is { CanGoBack: true } browser) browser.GoBack(); }
    private void BrowserForward_Click(object sender, RoutedEventArgs e) { if (CurrentBrowser is { CanGoForward: true } browser) browser.GoForward(); }
    private void BrowserHome_Click(object sender, RoutedEventArgs e) => NavigateBrowser(_settings.HomeUrl);
    private void BrowserHomeSettings_Click(object sender, RoutedEventArgs e)
    {
        string? currentUrl = CurrentBrowser?.Source?.AbsoluteUri ?? _activeBrowserTab?.Url;
        var dialog = new HomeAddressDialog(_settings.HomeUrl, currentUrl) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        _settings.HomeUrl = dialog.HomeUrl;
        try { _settings.Save(); } catch { }
    }
    private void BrowserRefresh_Click(object sender, RoutedEventArgs e) { if (CurrentBrowser?.CoreWebView2 != null) CurrentBrowser.Reload(); }

    private void ExternalProgramAttach_Click(object sender, RoutedEventArgs e)
    {
        var windows = ExternalWindowDock.GetAvailableWindows(new WindowInteropHelper(this).Handle);
        if (windows.Count == 0)
        {
            MessageBox.Show(this, "연결할 수 있는 다른 프로그램 창이 없습니다. 프로그램을 먼저 실행해주세요.",
                "프로그램 연결", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new ExternalWindowPickerDialog(windows) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.SelectedWindow == null) return;
        AttachExternalWindow(dialog.SelectedWindow);
    }

    private async void LaunchProgramShortcut_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not GeosangHelper.ProgramShortcutItem item) return;
        try
        {
            if (!File.Exists(item.ExecutablePath))
                throw new FileNotFoundException("등록한 프로그램 파일을 찾을 수 없습니다.", item.ExecutablePath);

            var before = ExternalWindowDock.GetAvailableWindows(new WindowInteropHelper(this).Handle)
                .Select(x => x.Handle).ToHashSet();
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = item.ExecutablePath,
                Arguments = item.Arguments,
                WorkingDirectory = Path.GetDirectoryName(item.ExecutablePath) ?? Environment.CurrentDirectory,
                UseShellExecute = true
            });
            string expectedProcess = Path.GetFileNameWithoutExtension(item.ExecutablePath);
            ExternalWindowInfo? target = null;
            for (int attempt = 0; attempt < 40 && target == null; attempt++)
            {
                await Task.Delay(250);
                var windows = ExternalWindowDock.GetAvailableWindows(new WindowInteropHelper(this).Handle);
                if (process != null) target = windows.FirstOrDefault(x => x.ProcessId == process.Id);
                target ??= windows.FirstOrDefault(x => !before.Contains(x.Handle) &&
                    string.Equals(x.ProcessName, expectedProcess, StringComparison.OrdinalIgnoreCase));
                target ??= windows.FirstOrDefault(x =>
                    string.Equals(x.ProcessName, expectedProcess, StringComparison.OrdinalIgnoreCase));
            }
            if (target == null)
                throw new InvalidOperationException("프로그램은 실행했지만 연결할 창을 찾지 못했습니다. 창이 나타난 뒤 '프로그램 연결'을 사용해주세요.");
            AttachExternalWindow(target);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "프로그램 실행 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void AttachExternalWindow(ExternalWindowInfo window)
    {
        try
        {
            BrowserViewsHost.Visibility = Visibility.Collapsed;
            ExternalProgramHost.Visibility = Visibility.Visible;
            ExternalProgramDetachButton.Visibility = Visibility.Visible;
            ExternalProgramHost.UpdateLayout();
            _externalWindowDock.Attach(window.Handle);
        }
        catch (Exception ex)
        {
            ShowBrowserArea();
            MessageBox.Show(this, ex.Message + "\n\n일반 창 모드로 실행되는 프로그램에서 사용할 수 있습니다.",
                "프로그램 연결 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ExternalProgramDetach_Click(object sender, RoutedEventArgs e) => _externalWindowDock.Detach();

    private void ShowBrowserArea()
    {
        if (_isClosing) return;
        ExternalProgramHost.Visibility = Visibility.Collapsed;
        BrowserViewsHost.Visibility = Visibility.Visible;
        ExternalProgramDetachButton.Visibility = Visibility.Collapsed;
    }

    private async void BrowserNewTab_Click(object sender, RoutedEventArgs e)
    {
        if (_browserReady) await CreateBrowserTabAsync(_settings.HomeUrl, select: true);
    }

    private void BrowserTab_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not BrowserTabEntry tab) return;
        SelectBrowserTab(tab);
    }

    private async void BrowserTabClose_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not BrowserTabEntry tab) return;
        bool wasActive = tab == _activeBrowserTab;
        int oldIndex = _browserTabs.IndexOf(tab);
        if (wasActive) _activeBrowserTab = null;
        _browserTabs.Remove(tab);
        BrowserViewsHost.Children.Remove(tab.View);
        tab.View.Dispose();
        if (_browserTabs.Count == 0)
        {
            await CreateBrowserTabAsync(_settings.HomeUrl, select: true);
        }
        else if (wasActive)
        {
            SelectBrowserTab(_browserTabs[Math.Clamp(oldIndex, 0, _browserTabs.Count - 1)]);
        }
        e.Handled = true;
    }

    private void SelectBrowserTab(BrowserTabEntry tab)
    {
        if (_isClosing || !_browserTabs.Contains(tab)) return;
        _activeBrowserTab = tab;
        foreach (var item in _browserTabs)
        {
            bool active = item == tab;
            item.IsActive = active;
            item.View.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        }
        BrowserAddress.Text = tab.Url;
    }

    private void BookmarkAdd_Click(object sender, RoutedEventArgs e)
    {
        var browser = CurrentBrowser;
        if (!_browserReady || browser?.Source == null || browser.CoreWebView2 == null) return;
        string url = browser.Source.AbsoluteUri;
        string title = browser.CoreWebView2.DocumentTitle;
        if (string.IsNullOrWhiteSpace(title)) title = browser.Source.Host;
        var dialog = new BookmarkDialog(title, url) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        string iconUrl = GetCurrentFaviconUrl(url);

        var existing = _bookmarks.FirstOrDefault(x => string.Equals(x.Url, url, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            existing.Name = dialog.BookmarkName;
            existing.IconUrl = iconUrl;
            BookmarkItems.Items.Refresh();
        }
        else
        {
            var item = new BookmarkItem { Name = dialog.BookmarkName, Url = url, IconUrl = iconUrl };
            _bookmarks.Add(item);
        }
        SaveBookmarks();
    }

    private void BookmarkTile_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is BookmarkItem item) NavigateBrowser(item.Url);
    }

    private void OpenImageShortcut_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not GeosangHelper.ImageShortcutItem item) return;
        try
        {
            var preview = new ImagePreviewWindow(item.Name, item.ImagePath) { Owner = this };
            preview.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "등록한 이미지를 열지 못했습니다. 환경설정에서 이미지를 다시 선택해주세요.\n" + ex.Message,
                "이미지 열기", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BookmarkContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu && menu.PlacementTarget is FrameworkElement target)
            menu.DataContext = target.DataContext;
    }

    private static BookmarkItem? BookmarkFromMenu(object sender) =>
        (sender as FrameworkElement)?.DataContext as BookmarkItem;

    private void BookmarkRenameMenu_Click(object sender, RoutedEventArgs e)
    {
        var item = BookmarkFromMenu(sender);
        if (item == null) return;
        var dialog = new BookmarkDialog(item.Name, item.Url, "북마크 이름 변경", "저장") { Owner = this };
        if (dialog.ShowDialog() != true) return;
        item.Name = dialog.BookmarkName;
        BookmarkItems.Items.Refresh();
        SaveBookmarks();
    }

    private void BookmarkMoveLeftMenu_Click(object sender, RoutedEventArgs e)
    {
        var item = BookmarkFromMenu(sender);
        if (item == null) return;
        int index = _bookmarks.IndexOf(item);
        if (index > 0) _bookmarks.Move(index, index - 1);
        SaveBookmarks();
    }

    private void BookmarkMoveRightMenu_Click(object sender, RoutedEventArgs e)
    {
        var item = BookmarkFromMenu(sender);
        if (item == null) return;
        int index = _bookmarks.IndexOf(item);
        if (index >= 0 && index < _bookmarks.Count - 1) _bookmarks.Move(index, index + 1);
        SaveBookmarks();
    }

    private void BookmarkDeleteMenu_Click(object sender, RoutedEventArgs e)
    {
        var item = BookmarkFromMenu(sender);
        if (item == null) return;
        _bookmarks.Remove(item);
        SaveBookmarks();
    }

    private string GetCurrentFaviconUrl(string pageUrl)
    {
        try
        {
            string favicon = CurrentBrowser?.CoreWebView2?.FaviconUri ?? string.Empty;
            if (Uri.TryCreate(favicon, UriKind.Absolute, out _)) return favicon;
        }
        catch { }
        return BuildDefaultFaviconUrl(pageUrl);
    }

    private static void EnsureBookmarkIcon(BookmarkItem bookmark)
    {
        if (string.IsNullOrWhiteSpace(bookmark.IconUrl))
            bookmark.IconUrl = BuildDefaultFaviconUrl(bookmark.Url);
    }

    private static string BuildDefaultFaviconUrl(string pageUrl)
    {
        try
        {
            var uri = new Uri(pageUrl);
            return new Uri(uri, "/favicon.ico").AbsoluteUri;
        }
        catch { return string.Empty; }
    }

    private void SaveBookmarks()
    {
        _settings.Bookmarks = _bookmarks.ToList();
        try { _settings.Save(); } catch { }
    }

    private static double NormalizeZoom(double value) =>
        double.IsFinite(value) ? Math.Clamp(Math.Round(value, 1), 0.5, 2.0) : 1.0;

    private void UpdateZoomDisplays()
    {
        BrowserZoomText.Text = $"{_settings.BrowserZoom:P0}";
        HelperZoomText.Text = $"{_settings.HelperZoom:P0}";
        MarketZoomText.Text = $"{_settings.MarketZoom:P0}";
        EmptyZoomText.Text = $"{_settings.EmptyZoom:P0}";
    }

    private void SetBrowserZoom(double value)
    {
        _settings.BrowserZoom = NormalizeZoom(value);
        foreach (var tab in _browserTabs) tab.View.ZoomFactor = _settings.BrowserZoom;
        UpdateZoomDisplays();
    }
    private void SetMarketZoom(double value)
    {
        _settings.MarketZoom = NormalizeZoom(value);
        if (_marketReady) MarketView.ZoomFactor = _settings.MarketZoom;
        UpdateZoomDisplays();
    }
    private void SetHelperZoom(double value)
    {
        _settings.HelperZoom = NormalizeZoom(value);
        ApplyHelperZoom();
        UpdateZoomDisplays();
    }
    private void SetEmptyZoom(double value)
    {
        _settings.EmptyZoom = NormalizeZoom(value);
        ApplyEmptyZoom();
        UpdateZoomDisplays();
    }

    private void ApplyHelperZoom()
    {
        if (HelperViewport.ActualWidth <= 0 || HelperViewport.ActualHeight <= 0) return;
        HelperPane.Width = HelperViewport.ActualWidth / _settings.HelperZoom;
        HelperPane.Height = HelperViewport.ActualHeight / _settings.HelperZoom;
        HelperPane.RenderTransformOrigin = new System.Windows.Point(0, 0);
        HelperPane.RenderTransform = new ScaleTransform(_settings.HelperZoom, _settings.HelperZoom);
    }

    private void ApplyEmptyZoom()
    {
        RightContent.LayoutTransform = new ScaleTransform(_settings.EmptyZoom, _settings.EmptyZoom);
        CpuTemperature.LayoutTransform = new ScaleTransform(_settings.EmptyZoom, _settings.EmptyZoom);
    }

    private void HelperViewport_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyHelperZoom();
    private void BrowserZoomOut_Click(object sender, RoutedEventArgs e) => SetBrowserZoom(_settings.BrowserZoom - 0.1);
    private void BrowserZoomIn_Click(object sender, RoutedEventArgs e) => SetBrowserZoom(_settings.BrowserZoom + 0.1);
    private void HelperZoomOut_Click(object sender, RoutedEventArgs e) => SetHelperZoom(_settings.HelperZoom - 0.1);
    private void HelperZoomIn_Click(object sender, RoutedEventArgs e) => SetHelperZoom(_settings.HelperZoom + 0.1);
    private void MarketZoomOut_Click(object sender, RoutedEventArgs e) => SetMarketZoom(_settings.MarketZoom - 0.1);
    private void MarketZoomIn_Click(object sender, RoutedEventArgs e) => SetMarketZoom(_settings.MarketZoom + 0.1);
    private void EmptyZoomOut_Click(object sender, RoutedEventArgs e) => SetEmptyZoom(_settings.EmptyZoom - 0.1);
    private void EmptyZoomIn_Click(object sender, RoutedEventArgs e) => SetEmptyZoom(_settings.EmptyZoom + 0.1);

    private void MarketSearch_Click(object sender, RoutedEventArgs e) => NavigateMarket(resetPage: true);
    private void MarketSearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { NavigateMarket(resetPage: true); e.Handled = true; }
    }
    private void MarketSortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded && _marketReady) NavigateMarket(resetPage: true);
    }
    private void MarketPrevious_Click(object sender, RoutedEventArgs e)
    {
        if (_settings.MarketPage <= 1) return;
        _settings.MarketPage--;
        NavigateMarket(resetPage: false);
    }
    private void MarketNext_Click(object sender, RoutedEventArgs e) { _settings.MarketPage++; NavigateMarket(resetPage: false); }
    private void MarketRefresh_Click(object sender, RoutedEventArgs e) { if (_marketReady) MarketView.Reload(); }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        _isClosing = true;
        _externalWindowDock.Dispose();
        CpuTemperature.Dispose();
        HelperPane.Dispose();
        _settings.WasMaximized = WindowState == WindowState.Maximized;
        if (WindowState == WindowState.Normal)
        {
            _settings.Width = ActualWidth;
            _settings.Height = ActualHeight;
            _settings.Left = Left;
            _settings.Top = Top;
        }
        _settings.TopPane = TopRow.Height.Value;
        _settings.BottomPane = BottomRow.Height.Value;
        _settings.HelperPane = HelperColumn.Width.Value;
        _settings.MarketPane = MarketColumn.Width.Value;
        _settings.EmptyPane = EmptyColumn.Width.Value;
        _settings.BrowserUrl = CurrentBrowser?.Source?.AbsoluteUri ?? BrowserAddress.Text;
        _settings.BrowserTabs = _browserTabs
            .Select(x => x.Url)
            .Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        _settings.ActiveBrowserTab = Math.Max(0, _activeBrowserTab == null ? 0 : _browserTabs.IndexOf(_activeBrowserTab));
        _settings.ServerId = ServerCombo.SelectedValue is int id ? id : 3;
        _settings.MarketSearch = MarketSearchBox.Text.Trim();
        _settings.MarketSort = SelectedMarketSort();
        _settings.Bookmarks = _bookmarks.ToList();
        try { _settings.Save(); } catch { }
        foreach (var tab in _browserTabs) tab.View.Dispose();
        MarketView.Dispose();
    }

    private void ShowStartupError(string message, Exception ex)
    {
        WriteErrorLog(message, ex);
        MessageBox.Show(this,
            message + "\n\nMicrosoft Edge WebView2 Runtime을 확인해주세요.\n\n" + ex.Message,
            "거상 통합 도우미", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static void WriteErrorLog(string area, Exception ex)
    {
        try
        {
            Directory.CreateDirectory(HubSettings.Folder);
            File.AppendAllText(Path.Combine(HubSettings.Folder, "error.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {area}\n{ex}\n\n");
        }
        catch { }
    }

    private sealed record ServerOption(int Id, string Name);

    private sealed class BrowserTabEntry : INotifyPropertyChanged
    {
        private string _title = "새 탭";
        private string _url = string.Empty;
        private bool _isActive;
        public required WebView2 View { get; init; }
        public string Title { get => _title; set { if (_title == value) return; _title = value; Changed(nameof(Title)); } }
        public string Url { get => _url; set { if (_url == value) return; _url = value; Changed(nameof(Url)); } }
        public bool IsActive { get => _isActive; set { if (_isActive == value) return; _isActive = value; Changed(nameof(IsActive)); } }
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
