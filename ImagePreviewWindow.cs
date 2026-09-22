using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Button = System.Windows.Controls.Button;
using Image = System.Windows.Controls.Image;
using Orientation = System.Windows.Controls.Orientation;

namespace GeosangHub;

public sealed class ImagePreviewWindow : Window
{
    private readonly BitmapImage _bitmap;
    private readonly Grid _viewer;
    private bool _actualSize;

    public ImagePreviewWindow(string name, string path)
    {
        _bitmap = new BitmapImage();
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            _bitmap.BeginInit();
            _bitmap.CacheOption = BitmapCacheOption.OnLoad;
            _bitmap.StreamSource = stream;
            _bitmap.EndInit();
            _bitmap.Freeze();
        }

        Title = name;
        Width = 900;
        Height = 700;
        MinWidth = 320;
        MinHeight = 240;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.Black;

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Content = layout;
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(48, 41, 37)) };
        var fit = new Button { Content = "화면에 맞춤", Margin = new Thickness(6), Padding = new Thickness(8, 3, 8, 3) };
        fit.Click += (_, _) => ShowFit();
        var actual = new Button { Content = "원본 크기", Margin = new Thickness(0, 6, 6, 6), Padding = new Thickness(8, 3, 8, 3) };
        actual.Click += (_, _) => ShowActualSize();
        toolbar.Children.Add(fit);
        toolbar.Children.Add(actual);
        layout.Children.Add(toolbar);

        _viewer = new Grid();
        Grid.SetRow(_viewer, 1);
        layout.Children.Add(_viewer);
        ShowFit();
    }

    private void ShowFit()
    {
        _actualSize = false;
        _viewer.Children.Clear();
        _viewer.Children.Add(new Image { Source = _bitmap, Stretch = Stretch.Uniform });
    }

    private void ShowActualSize()
    {
        if (_actualSize) return;
        _actualSize = true;
        _viewer.Children.Clear();
        var image = new Image { Source = _bitmap, Stretch = Stretch.None, Width = _bitmap.Width, Height = _bitmap.Height };
        _viewer.Children.Add(new ScrollViewer { Content = image, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
    }
}
