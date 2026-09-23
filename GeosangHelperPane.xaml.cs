using System.IO;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
namespace GeosangHelper;
public partial class GeosangHelperPane : System.Windows.Controls.UserControl, IDisposable
{
    private AppState state = new();
    private readonly DispatcherTimer tick = new() { Interval = TimeSpan.FromSeconds(1) };
    private GameNetworkMonitor? gameMonitor;
    private DateTimeOffset? battleStartedAt;
    private bool ready;
    private Window? HostWindow => Window.GetWindow(this);
    public ObservableCollection<ImageShortcutItem> ImageShortcuts => state.ImageShortcuts;
    public ObservableCollection<ProgramShortcutItem> ProgramShortcuts => state.ProgramShortcuts;

    public GeosangHelperPane()
    {
        InitializeComponent();
        try { state = Storage.Load(); }
        catch (Exception ex) { MessageBox.Show($"저장 파일을 읽지 못했습니다. 원본 보호를 위해 앱을 종료합니다.\n{Storage.FilePath}\n.bak 파일로 복구할 수 있습니다.\n{ex.Message}"); Application.Current.Shutdown(); return; }
        TopBox.IsChecked = state.AlwaysOnTop;
        SizeLockBox.IsChecked = state.SizeLocked;
        FontSizeSlider.Value = state.FontSize;
        PanelOpacitySlider.Value = state.PanelOpacity;
        ApplyAppearance();
        try { StartupBox.IsChecked = StartupService.Enabled; } catch (Exception ex) { Status.Text = ex.Message; }
        OverviewTimerList.ItemsSource = TimerSettingsList.ItemsSource = state.Timers;
        OverviewCheckList.ItemsSource = CheckSettingsList.ItemsSource = state.Checklist;
        ImageShortcutSettingsList.ItemsSource = state.ImageShortcuts;
        ProgramShortcutSettingsList.ItemsSource = state.ProgramShortcuts;
        BattleList.ItemsSource = state.Battles;
        MarketList.ItemsSource = state.MarketListings;
        BattleLabelBox.Text = state.BattleLabel;
        state.GameMonitoringEnabled = false;
        GameMonitorBox.IsChecked = false;
        MainTabs.SelectedIndex = state.SelectedTab is 0 or 1 or 2 or 6 ? state.SelectedTab : 0;
        UpdateEmptyMessages();
        UpdateGameSummaries();
        ready = true;
        tick.Tick += (_, _) =>
        {
            foreach (var timer in state.Timers) timer.Refresh();
            if (battleStartedAt.HasValue) BattleLiveText.Text = $"전투 측정 중 · {(DateTimeOffset.Now - battleStartedAt.Value).TotalSeconds:0.0}초";
        };
        tick.Start();
        Loaded += (_, _) =>
        {
            if (HostWindow != null)
            {
                HostWindow.Topmost = state.AlwaysOnTop;
            }
        };
    }
    private bool Save()
    {
        if (!ready) return true;
        state.SelectedTab = MainTabs.SelectedIndex;
        try { Storage.Save(state); Status.Text = "저장됨 · " + DateTime.Now.ToString("HH:mm:ss"); return true; }
        catch (Exception ex) { Status.Text = "저장 실패: " + ex.Message; MessageBox.Show("변경 내용을 저장하지 못했습니다. 저장 위치와 권한을 확인해주세요.\n" + ex.Message); return false; }
    }
    private void DragHeader(object sender, MouseButtonEventArgs e) { if (e.OriginalSource is Button) return; if (e.LeftButton == MouseButtonState.Pressed) { HostWindow?.DragMove(); Save(); } }
    private void CloseApp(object sender, RoutedEventArgs e) => HostWindow?.Close();
    private void MinimizeApp(object sender, RoutedEventArgs e) { if (HostWindow != null) HostWindow.WindowState = WindowState.Minimized; }
    private void TopChanged(object sender, RoutedEventArgs e)
    {
        state.AlwaysOnTop = TopBox.IsChecked == true;
        if (HostWindow != null) HostWindow.Topmost = state.AlwaysOnTop;
        Save();
    }
    private void SizeLockChanged(object sender, RoutedEventArgs e)
    {
        state.SizeLocked = SizeLockBox.IsChecked == true;
        Save();
    }
    private void FontSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!ready) return;
        state.FontSize = Math.Round(FontSizeSlider.Value);
        ApplyTextAppearance(); Save();
    }
    private void PanelOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!ready) return;
        state.PanelOpacity = PanelOpacitySlider.Value;
        ApplyPanelAppearance(); Save();
    }
    private void ChooseFontColor(object sender, RoutedEventArgs e)
    {
        var current = (MediaColor)MediaColorConverter.ConvertFromString(state.FontColor);
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(current.R, current.G, current.B)
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        state.FontColor = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        ApplyTextAppearance(); Save();
    }
    private void ChooseBackground(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "배경 이미지 선택", Filter = "이미지 파일|*.png;*.jpg;*.jpeg;*.bmp|모든 파일|*.*" };
        if (dialog.ShowDialog(HostWindow) != true) return;
        try
        {
            Directory.CreateDirectory(Storage.Folder);
            string extension = Path.GetExtension(dialog.FileName).ToLowerInvariant();
            string destination = Path.Combine(Storage.Folder, "custom-background" + extension);
            if (!string.Equals(Path.GetFullPath(dialog.FileName), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
                File.Copy(dialog.FileName, destination, true);
            state.BackgroundImagePath = destination;
            ApplyBackground(); Save();
        }
        catch (Exception ex) { MessageBox.Show(HostWindow, "배경 이미지를 적용하지 못했습니다.\n" + ex.Message); }
    }
    private void ResetBackground(object sender, RoutedEventArgs e)
    {
        state.BackgroundImagePath = null; ApplyBackground(); Save();
    }
    private void TabChanged(object sender, SelectionChangedEventArgs e) { if (ready && e.Source == MainTabs) Save(); }
    private void StartupChanged(object sender, RoutedEventArgs e)
    {
        try { StartupService.Set(StartupBox.IsChecked == true); Status.Text = "자동 실행 설정을 적용했습니다."; }
        catch (Exception ex) { StartupBox.IsChecked = !(StartupBox.IsChecked == true); MessageBox.Show(ex.Message); }
    }
    private void GameMonitorChanged(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        state.GameMonitoringEnabled = GameMonitorBox.IsChecked == true;
        if (state.GameMonitoringEnabled) StartGameMonitor(); else StopGameMonitor();
        Save();
    }
    private void StartGameMonitor()
    {
        if (gameMonitor != null) return;
        try
        {
            gameMonitor = new GameNetworkMonitor();
            gameMonitor.MessageReceived += OnGameMessage;
            gameMonitor.Start();
            GameMonitorStatus.Text = $"자동 인식 작동 중 · 네트워크 장치 {gameMonitor.DeviceCount}개";
            Status.Text = "거상 전투·시세 자동 인식 작동 중";
        }
        catch (Exception ex)
        {
            gameMonitor?.Dispose(); gameMonitor = null;
            GameMonitorStatus.Text = "자동 인식 시작 실패 · " + ex.Message;
            Status.Text = "Npcap 또는 관리자 권한을 확인해주세요.";
        }
    }
    private void StopGameMonitor()
    {
        gameMonitor?.Dispose(); gameMonitor = null; battleStartedAt = null;
        GameMonitorStatus.Text = "자동 인식 꺼짐";
        BattleLiveText.Text = "전투 대기 중";
    }
    private void OnGameMessage(GameServerMessage message)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (message.Opcode == 0x4D && message.Data.Length == 35 && !battleStartedAt.HasValue)
            {
                battleStartedAt = message.ReceivedAt;
                BattleLiveText.Text = "전투 측정 시작";
            }
            else if (message.Opcode == 0x77 && battleStartedAt.HasValue)
            {
                var elapsed = message.ReceivedAt - battleStartedAt.Value;
                if (elapsed.TotalSeconds is >= 2 and <= 600)
                {
                    state.Battles.Insert(0, new BattleRecord
                    {
                        Name = state.BattleLabel,
                        StartedAtUnixMs = battleStartedAt.Value.ToUnixTimeMilliseconds(),
                        EndedAtUnixMs = message.ReceivedAt.ToUnixTimeMilliseconds()
                    });
                    while (state.Battles.Count > 200) state.Battles.RemoveAt(state.Battles.Count - 1);
                    BattleLiveText.Text = $"최근 전투 · {elapsed.TotalSeconds:0.000}초";
                    Status.Text = $"전투 완료 · {state.BattleLabel} · {elapsed.TotalSeconds:0.000}초";
                }
                battleStartedAt = null;
                UpdateGameSummaries(); Save();
            }
            var listings = GameDataDecoder.DecodeMarket(message);
            if (listings.Count > 0)
            {
                state.MarketListings.Clear();
                foreach (var listing in listings.OrderBy(x => x.UnitPrice)) state.MarketListings.Add(listing);
                UpdateGameSummaries(); Save();
            }
        });
    }
    private void SaveBattleLabel(object sender, RoutedEventArgs e)
    {
        string value = BattleLabelBox.Text.Trim();
        if (value.Length == 0) return;
        state.BattleLabel = value; Save(); UpdateGameSummaries();
    }
    private void ClearBattles(object sender, RoutedEventArgs e) { state.Battles.Clear(); UpdateGameSummaries(); Save(); }
    private void ClearMarket(object sender, RoutedEventArgs e) { state.MarketListings.Clear(); UpdateGameSummaries(); Save(); }
    private void UpdateGameSummaries()
    {
        if (state.Battles.Count == 0) OverviewBattleText.Text = "아직 측정된 전투가 없습니다.";
        else
        {
            double average = state.Battles.Average(x => x.DurationSeconds);
            OverviewBattleText.Text = $"최근 {state.Battles[0].DurationSeconds:0.000}초 · 평균 {average:0.000}초 · {state.Battles.Count}회";
        }
        if (state.MarketListings.Count == 0)
        {
            OverviewMarketText.Text = "아직 인식된 시세가 없습니다.";
            MarketSummaryText.Text = "검색 대기 중";
        }
        else
        {
            var lowest = state.MarketListings.MinBy(x => x.UnitPrice)!;
            OverviewMarketText.Text = $"{lowest.ItemName} 최저 {lowest.UnitPrice:N0}원 · {state.MarketListings.Count}개 매물";
            MarketSummaryText.Text = $"{lowest.ItemName} · 최저 {lowest.UnitPrice:N0}원";
        }
        RecordSummaryText.Text = $"저장된 전투 {state.Battles.Count}회\n현재 시세 목록 {state.MarketListings.Count}개\n저장 위치: {Storage.FilePath}";
    }
    private void AddTimer(object sender, RoutedEventArgs e)
    {
        var dialog = new TimerDialog(null);
        if (HostWindow != null) dialog.Owner = HostWindow;
        if (dialog.ShowDialog() == true) { state.Timers.Add(new() { Name = dialog.TimerName, DurationSeconds = dialog.DurationSeconds }); UpdateEmptyMessages(); Save(); }
    }
    private static MissionTimer TimerFrom(object sender) => (MissionTimer)((FrameworkElement)sender).DataContext;
    private void EditTimer(object sender, RoutedEventArgs e)
    {
        var timer = TimerFrom(sender); var dialog = new TimerDialog(timer);
        if (HostWindow != null) dialog.Owner = HostWindow;
        if (dialog.ShowDialog() == true) { timer.Name = dialog.TimerName; timer.DurationSeconds = dialog.DurationSeconds; timer.Refresh(); Save(); }
    }
    private void StartTimer(object sender, RoutedEventArgs e) { TimerFrom(sender).Start(); Save(); }
    private void CompleteTimer(object sender, RoutedEventArgs e) { TimerFrom(sender).Complete(); Save(); }
    private void DeleteTimer(object sender, RoutedEventArgs e) { state.Timers.Remove(TimerFrom(sender)); UpdateEmptyMessages(); Save(); }
    private void AddCheck(object sender, RoutedEventArgs e)
    {
        string name = CheckName.Text.Trim(); if (name.Length == 0) return;
        state.Checklist.Add(new() { Name = name }); CheckName.Clear(); UpdateEmptyMessages(); Save();
    }
    private void CheckKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) AddCheck(sender, e); }
    private void DeleteCheck(object sender, RoutedEventArgs e) { state.Checklist.Remove((CheckItem)((FrameworkElement)sender).DataContext); UpdateEmptyMessages(); Save(); }
    private void CheckChanged(object sender, RoutedEventArgs e) => Save();
    private void AddImageShortcut(object sender, RoutedEventArgs e)
    {
        var dialog = new ImageShortcutDialog { Owner = HostWindow };
        if (dialog.ShowDialog() != true) return;
        string? copiedPath = null;
        try
        {
            copiedPath = CopyShortcutImage(dialog.ImagePath);
            var item = new ImageShortcutItem { Name = dialog.ShortcutName, ImagePath = copiedPath };
            state.ImageShortcuts.Add(item);
            if (!Save())
            {
                state.ImageShortcuts.Remove(item);
                File.Delete(copiedPath);
            }
        }
        catch (Exception ex)
        {
            if (copiedPath != null && File.Exists(copiedPath)) File.Delete(copiedPath);
            MessageBox.Show(HostWindow, "이미지를 등록하지 못했습니다.\n" + ex.Message);
        }
    }
    private void AddProgramShortcut(object sender, RoutedEventArgs e)
    {
        var dialog = new ProgramShortcutDialog { Owner = HostWindow };
        if (dialog.ShowDialog() != true) return;
        var item = new ProgramShortcutItem
        {
            Name = dialog.ShortcutName,
            ExecutablePath = dialog.ExecutablePath,
            Arguments = dialog.Arguments
        };
        state.ProgramShortcuts.Add(item);
        if (!Save()) state.ProgramShortcuts.Remove(item);
    }
    private void EditProgramShortcut(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not ProgramShortcutItem item) return;
        var dialog = new ProgramShortcutDialog(item.Name, item.ExecutablePath, item.Arguments) { Owner = HostWindow };
        if (dialog.ShowDialog() != true) return;
        int index = state.ProgramShortcuts.IndexOf(item);
        if (index < 0) return;
        var replacement = new ProgramShortcutItem
        {
            Name = dialog.ShortcutName,
            ExecutablePath = dialog.ExecutablePath,
            Arguments = dialog.Arguments
        };
        state.ProgramShortcuts[index] = replacement;
        if (!Save()) state.ProgramShortcuts[index] = item;
    }
    private void DeleteProgramShortcut(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not ProgramShortcutItem item) return;
        int index = state.ProgramShortcuts.IndexOf(item);
        if (index < 0) return;
        state.ProgramShortcuts.RemoveAt(index);
        if (!Save()) state.ProgramShortcuts.Insert(index, item);
    }
    private void EditImageShortcut(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not ImageShortcutItem item) return;
        var dialog = new ImageShortcutDialog(item.Name, item.ImagePath) { Owner = HostWindow };
        if (dialog.ShowDialog() != true) return;
        string? copiedPath = null;
        try
        {
            if (dialog.ImageChanged) copiedPath = CopyShortcutImage(dialog.ImagePath);
            int index = state.ImageShortcuts.IndexOf(item);
            if (index < 0) return;
            state.ImageShortcuts[index] = new ImageShortcutItem
            {
                Name = dialog.ShortcutName,
                ImagePath = copiedPath ?? item.ImagePath
            };
            if (!Save())
            {
                state.ImageShortcuts[index] = item;
                if (copiedPath != null) File.Delete(copiedPath);
            }
        }
        catch (Exception ex)
        {
            if (copiedPath != null && File.Exists(copiedPath)) File.Delete(copiedPath);
            MessageBox.Show(HostWindow, "이미지 버튼을 수정하지 못했습니다.\n" + ex.Message);
        }
    }
    private void DeleteImageShortcut(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not ImageShortcutItem item) return;
        int index = state.ImageShortcuts.IndexOf(item);
        if (index < 0) return;
        state.ImageShortcuts.RemoveAt(index);
        if (!Save()) state.ImageShortcuts.Insert(index, item);
    }
    private static string CopyShortcutImage(string sourcePath)
    {
        using (var stream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            if (bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0) throw new InvalidDataException("이미지 파일을 읽지 못했습니다.");
        }
        string folder = Path.Combine(Storage.Folder, "shortcut-images");
        Directory.CreateDirectory(folder);
        string destination = Path.Combine(folder, Guid.NewGuid().ToString("N") + Path.GetExtension(sourcePath).ToLowerInvariant());
        File.Copy(sourcePath, destination);
        return destination;
    }
    private void UpdateEmptyMessages()
    {
        NoTimersText.Visibility = state.Timers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        NoChecksText.Visibility = state.Checklist.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    private void ApplyAppearance() { ApplyTextAppearance(); ApplyPanelAppearance(); ApplyBackground(); }
    private void ApplyTextAppearance()
    {
        MediaColor color;
        try { color = (MediaColor)MediaColorConverter.ConvertFromString(state.FontColor); }
        catch { color = MediaColor.FromRgb(50, 30, 24); state.FontColor = "#321E18"; }
        Application.Current.Resources["AppTextBrush"] = new SolidColorBrush(color);
        Application.Current.Resources["MutedTextBrush"] = new SolidColorBrush(MediaColor.FromArgb(190, color.R, color.G, color.B));
        Application.Current.Resources["BaseFontSize"] = state.FontSize;
        Application.Current.Resources["HeadingFontSize"] = state.FontSize + 4;
        Application.Current.Resources["SmallFontSize"] = Math.Max(9, state.FontSize - 3);
        Application.Current.Resources["TimerFontSize"] = state.FontSize + 2;
        FontColorSample.Background = new SolidColorBrush(color);
        FontSizeValue.Text = $"{state.FontSize:0} pt";
    }
    private void ApplyPanelAppearance()
    {
        byte panelAlpha = (byte)Math.Round(255 * state.PanelOpacity);
        byte cardAlpha = (byte)Math.Round(255 * Math.Min(0.92, state.PanelOpacity + 0.22));
        Application.Current.Resources["PanelBrush"] = new SolidColorBrush(MediaColor.FromArgb(panelAlpha, 255, 249, 242));
        Application.Current.Resources["CardBrush"] = new SolidColorBrush(MediaColor.FromArgb(cardAlpha, 255, 250, 244));
        PanelOpacityValue.Text = $"{state.PanelOpacity:P0}";
    }
    private void ApplyBackground()
    {
        try
        {
            BitmapImage bitmap;
            if (!string.IsNullOrWhiteSpace(state.BackgroundImagePath) && File.Exists(state.BackgroundImagePath))
            {
                using var stream = new FileStream(state.BackgroundImagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
                BackgroundPathText.Text = "사용자 배경 · " + Path.GetFileName(state.BackgroundImagePath);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(state.BackgroundImagePath)) state.BackgroundImagePath = null;
                bitmap = new BitmapImage(new Uri("pack://application:,,,/Assets/hanbok-bg.png"));
                BackgroundPathText.Text = "기본 내장 이미지";
            }
            Background = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill, AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center };
        }
        catch (Exception ex)
        {
            Background = new SolidColorBrush(MediaColor.FromRgb(245, 235, 225));
            BackgroundPathText.Text = "이미지를 읽지 못했습니다 · " + ex.Message;
        }
    }

    public void Dispose()
    {
        Save();
        gameMonitor?.Dispose();
        gameMonitor = null;
        tick.Stop();
    }
}
