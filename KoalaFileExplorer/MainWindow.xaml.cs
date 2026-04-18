using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KoalaFileExplorer.Models;
using KoalaFileExplorer.Services;
using KoalaFileExplorer.ViewModels;

namespace KoalaFileExplorer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly DispatcherTimer _timer;
    private bool _isDraggingSlider;
    private bool _isMediaLoaded;

    private readonly string[] _tagColors =
    {
        "#2196F3", "#E91E63", "#4CAF50", "#FF9800", "#9C27B0",
        "#00BCD4", "#F44336", "#8BC34A", "#FF5722", "#607D8B"
    };
    private int _colorIndex;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += Timer_Tick;
        MediaPlayer.Volume = VolumeSlider.Value;
    }

    // ── File Tree ──────────────────────────────────────────────────────────
    private void FileTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is DriveNode node)
        {
            _vm.LoadChildren(node);
            _vm.NavigateTo(node.FullPath);
            LoadThumbnailsAsync();
        }
    }

    // ── File List ──────────────────────────────────────────────────────────
    private void FileListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        StopMedia();
        _vm.SelectedFile = FileListView.SelectedItem as FileItem;
        // A: auto-play on selection
        if (_vm.SelectedFile?.IsMediaFile == true)
            PlayMedia();
    }

    private void FileListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var file = _vm.SelectedFile;
        if (file == null) return;

        if (file.IsDirectory)
        {
            _vm.NavigateTo(file.FullPath);
            LoadThumbnailsAsync();
        }
        else
        {
            // Double-click opens in external player
            _vm.OpenFileExternal(file.FullPath);
        }
    }

    // ── Keyboard shortcuts (1-5 = star tags, 0 = clear) ───────────────────
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Keyboard.FocusedElement is TextBox) return;

        switch (e.Key)
        {
            case Key.D0: _vm.RemoveAllTagsFromFile(); e.Handled = true; break;
            case Key.D1: _vm.AddStarTag("1star"); e.Handled = true; break;
            case Key.D2: _vm.AddStarTag("2star"); e.Handled = true; break;
            case Key.D3: _vm.AddStarTag("3star"); e.Handled = true; break;
            case Key.D4: _vm.AddStarTag("4star"); e.Handled = true; break;
            case Key.D5: _vm.AddStarTag("5star"); e.Handled = true; break;
        }
    }

    // ── Async thumbnail loading ────────────────────────────────────────────
    private void LoadThumbnailsAsync()
    {
        var mediaFiles = _vm.Files.Where(f => f.IsMediaFile && !f.IsDirectory).ToList();
        foreach (var file in mediaFiles)
        {
            var captured = file;
            _ = Task.Run(async () =>
            {
                var thumb = await ThumbnailService.GetAsync(captured.FullPath, 100);
                if (thumb != null)
                    Dispatcher.Invoke(() => captured.Thumbnail = thumb);
            });
        }
    }

    // ── Media Player ──────────────────────────────────────────────────────
    private void PlayPauseBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!_isMediaLoaded) { PlayMedia(); return; }

        if (_vm.IsPlaying)
        {
            MediaPlayer.Pause();
            _vm.IsPlaying = false;
            PlayPauseBtn.Content = "▶ Play";
            _timer.Stop();
        }
        else
        {
            MediaPlayer.Play();
            _vm.IsPlaying = true;
            PlayPauseBtn.Content = "⏸ Pause";
            _timer.Start();
        }
    }

    private void StopBtn_Click(object sender, RoutedEventArgs e) => StopMedia();

    private void StopMedia()
    {
        MediaPlayer.Stop();
        MediaPlayer.Source = null;
        _vm.IsPlaying = false;
        _isMediaLoaded = false;
        PlayPauseBtn.Content = "▶ Play";
        _timer.Stop();
        SeekSlider.Value = 0;
        TimeDisplay.Text = "0:00 / 0:00";
    }

    private void PlayMedia()
    {
        var file = _vm.SelectedFile;
        if (file?.IsMediaFile != true) return;
        MediaPlayer.Source = new Uri(file.FullPath);
        MediaPlayer.Play();
        _vm.IsPlaying = true;
        _isMediaLoaded = true;
        PlayPauseBtn.Content = "⏸ Pause";
        _timer.Start();
    }

    private void MediaPlayer_MediaOpened(object sender, RoutedEventArgs e)
    {
        _isMediaLoaded = true;
        if (MediaPlayer.NaturalDuration.HasTimeSpan)
            SeekSlider.Maximum = MediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
    }

    private void MediaPlayer_MediaEnded(object sender, RoutedEventArgs e)
    {
        _vm.IsPlaying = false;
        _isMediaLoaded = false;
        PlayPauseBtn.Content = "▶ Play";
        _timer.Stop();
        SeekSlider.Value = 0;
    }

    private void MediaPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        _vm.StatusText = $"Media error: {e.ErrorException?.Message ?? "Unknown error"}";
        StopMedia();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (_isDraggingSlider || !_isMediaLoaded) return;
        if (MediaPlayer.NaturalDuration.HasTimeSpan)
        {
            var pos = MediaPlayer.Position.TotalSeconds;
            var dur = MediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
            SeekSlider.Value = pos;
            TimeDisplay.Text = $"{FormatTime(pos)} / {FormatTime(dur)}";
        }
    }

    private void SeekSlider_MouseDown(object sender, MouseButtonEventArgs e) => _isDraggingSlider = true;

    private void SeekSlider_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingSlider = false;
        if (_isMediaLoaded) MediaPlayer.Position = TimeSpan.FromSeconds(SeekSlider.Value);
    }

    private void SeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isDraggingSlider && _isMediaLoaded)
            MediaPlayer.Position = TimeSpan.FromSeconds(SeekSlider.Value);
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        => MediaPlayer.Volume = VolumeSlider.Value;

    private static string FormatTime(double s)
    {
        var t = TimeSpan.FromSeconds(s);
        return t.Hours > 0 ? $"{t.Hours}:{t.Minutes:D2}:{t.Seconds:D2}" : $"{t.Minutes}:{t.Seconds:D2}";
    }

    // ── External player ────────────────────────────────────────────────────
    private void OpenExternalBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedFile != null)
            _vm.OpenFileExternal(_vm.SelectedFile.FullPath);
    }

    // ── Tag buttons ────────────────────────────────────────────────────────
    private void CreateTagBtn_Click(object sender, RoutedEventArgs e)
    {
        var name = NewTagNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Please enter a customer tag name.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var color = ((SolidColorBrush)TagColorRect.Fill).Color;
        var hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        try
        {
            var tag = _vm._tagService.CreateTag(name, hex);
            _vm.AllTags.Add(tag);
            NewTagNameBox.Clear();
            _vm.StatusText = $"Tag '{name}' created.";
            _colorIndex = (_colorIndex + 1) % _tagColors.Length;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void NewTagNameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) CreateTagBtn_Click(sender, new RoutedEventArgs());
    }

    private void TagColorRect_Click(object sender, MouseButtonEventArgs e)
    {
        _colorIndex = (_colorIndex + 1) % _tagColors.Length;
        var color = (Color)ColorConverter.ConvertFromString(_tagColors[_colorIndex]);
        TagColorRect.Fill = new SolidColorBrush(color);
    }

    private void TagFileBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedFile == null)
        {
            MessageBox.Show("Please select a file first.", "No File Selected",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (((Button)sender).Tag is CustomerTag tag)
            _vm.AddTagToFilePublic(tag);
    }

    private void ShowTagFilesBtn_Click(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).Tag is CustomerTag tag)
        {
            _vm.ShowFilesWithTag(tag);
            LoadThumbnailsAsync();
        }
    }

    private void DeleteTagBtn_Click(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).Tag is CustomerTag tag)
        {
            var r = MessageBox.Show($"Delete tag '{tag.Name}'? This will remove it from all files.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r == MessageBoxResult.Yes) _vm.DeleteTag(tag);
        }
    }

    private void RemoveTagBtn_Click(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).Tag is CustomerTag tag)
            _vm.RemoveTagFromFilePublic(tag);
    }
}
