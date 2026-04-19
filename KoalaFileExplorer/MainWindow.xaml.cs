using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KoalaFileExplorer.Models;
using KoalaFileExplorer.Services;
using KoalaFileExplorer.ViewModels;
using KoalaFileExplorer.Views;
using Microsoft.Win32;

namespace KoalaFileExplorer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly DispatcherTimer _timer;
    private bool _isDraggingSlider;
    private bool _isMediaLoaded;
    private string? _currentlyPlayingPath;
    private ListView? _contextTargetList;

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

        // Build and assign context menus
        var cm1 = BuildFileContextMenu();
        var cm2 = BuildFileContextMenu();
        FileListView.ContextMenu = cm1;
        FindResultsList.ContextMenu = cm2;
        cm1.Opened += ContextMenu_Opened;
        cm2.Opened += ContextMenu_Opened;
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
        _vm.SelectedFile = FileListView.SelectedItem as FileItem;
        var file = _vm.SelectedFile;
        // Single click: auto-play if media file changes
        if (file?.IsMediaFile == true && file.FullPath != _currentlyPlayingPath)
            PlayMedia();
        // Non-media click: keep current playback running
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
            _vm.OpenFileExternal(file.FullPath);
        }
    }

    // ── Context menu (right-click) ─────────────────────────────────────────
    private void FileList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListView lv) return;
        _contextTargetList = lv;
        var item = (e.OriginalSource as FrameworkElement)?.DataContext as FileItem;
        if (item != null && !lv.SelectedItems.Contains(item))
        {
            lv.SelectedItems.Clear();
            lv.SelectedItem = item;
        }
    }

    private ContextMenu BuildFileContextMenu()
    {
        var menu = new ContextMenu();

        var playItem = new MenuItem { Header = "▶  Play Preview" };
        playItem.Click += (_, _) =>
        {
            var f = _contextTargetList?.SelectedItem as FileItem;
            if (f?.IsMediaFile == true) { _vm.SelectedFile = f; PlayMedia(); }
        };

        var extItem = new MenuItem { Header = "📂  Open with External Player" };
        extItem.Click += (_, _) =>
        {
            var f = _contextTargetList?.SelectedItem as FileItem;
            if (f != null) _vm.OpenFileExternal(f.FullPath);
        };

        var explorerItem = new MenuItem { Header = "📁  Show in Explorer" };
        explorerItem.Click += (_, _) =>
        {
            var f = _contextTargetList?.SelectedItem as FileItem;
            if (f != null) ShowInExplorer(f.FullPath);
        };

        var tagsMenu = new MenuItem { Header = "🏷  Add Tag", Tag = "TagsMenu" };

        var removeTagsItem = new MenuItem { Header = "🗑  Remove All Tags" };
        removeTagsItem.Click += (_, _) =>
        {
            foreach (var f in GetContextSelectedFiles())
                _vm.RemoveAllTagsFromFilePath(f.FullPath);
        };

        var copyItem = new MenuItem { Header = "📋  Copy Path" };
        copyItem.Click += (_, _) =>
        {
            var f = _contextTargetList?.SelectedItem as FileItem;
            if (f != null) Clipboard.SetText(f.FullPath);
        };

        var deleteItem = new MenuItem { Header = "🗑  Delete" };
        deleteItem.Click += (_, _) => DeleteFiles(GetContextSelectedFiles().ToList());

        menu.Items.Add(playItem);
        menu.Items.Add(extItem);
        menu.Items.Add(explorerItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(tagsMenu);
        menu.Items.Add(removeTagsItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(copyItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(deleteItem);
        return menu;
    }

    private void ContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu) return;
        var tagsMenu = menu.Items.OfType<MenuItem>().FirstOrDefault(m => m.Tag?.ToString() == "TagsMenu");
        if (tagsMenu == null) return;
        tagsMenu.Items.Clear();
        foreach (var tag in _vm.AllTags)
        {
            var item = new MenuItem { Header = tag.Name, Tag = tag };
            item.Click += (s, _) =>
            {
                foreach (var f in GetContextSelectedFiles())
                {
                    _vm.SelectedFile = f;
                    _vm.AddTagToFilePublic((s as MenuItem)?.Tag as CustomerTag);
                }
            };
            tagsMenu.Items.Add(item);
        }
    }

    private IEnumerable<FileItem> GetContextSelectedFiles()
        => (_contextTargetList?.SelectedItems.OfType<FileItem>() ?? Enumerable.Empty<FileItem>())
           .Where(f => !f.IsDirectory);

    private static void ShowInExplorer(string path)
    {
        try { Process.Start("explorer.exe", $"/select,\"{path}\""); } catch { }
    }

    // ── Delete ─────────────────────────────────────────────────────────────
    private void DeleteFiles(List<FileItem> files)
    {
        var toDelete = files.Where(f => !f.IsDirectory).ToList();
        if (!toDelete.Any()) return;
        var msg = toDelete.Count == 1
            ? $"Permanently delete '{toDelete[0].Name}'?"
            : $"Permanently delete {toDelete.Count} files?";
        if (MessageBox.Show(msg, "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning)
            != MessageBoxResult.Yes) return;
        _vm.DeleteFiles(toDelete);
    }

    // ── Sort buttons ───────────────────────────────────────────────────────
    private void SortName_Click(object sender, RoutedEventArgs e) => _vm.SetSort("Name");
    private void SortDate_Click(object sender, RoutedEventArgs e) => _vm.SetSort("Date");
    private void SortSize_Click(object sender, RoutedEventArgs e) => _vm.SetSort("Size");
    private void SortType_Click(object sender, RoutedEventArgs e) => _vm.SetSort("Type");

    // ── Keyboard shortcuts ─────────────────────────────────────────────────
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var mods = Keyboard.Modifiers;
        var action = _vm.KeyBindings.GetAction(key, mods);
        if (action == null) return;

        // Non-modifier shortcuts skip TextBox focus
        if (mods == ModifierKeys.None && Keyboard.FocusedElement is TextBox) return;

        switch (action)
        {
            case "ClearTags":   _vm.RemoveAllTagsFromFile(); e.Handled = true; break;
            case "Tag1Star":    _vm.AddStarTag("1star"); e.Handled = true; break;
            case "Tag2Star":    _vm.AddStarTag("2star"); e.Handled = true; break;
            case "Tag3Star":    _vm.AddStarTag("3star"); e.Handled = true; break;
            case "Tag4Star":    _vm.AddStarTag("4star"); e.Handled = true; break;
            case "Tag5Star":    _vm.AddStarTag("5star"); e.Handled = true; break;
            case "FastForward": FastForward(); e.Handled = true; break;
            case "Rewind":      FastForward(-1); e.Handled = true; break;
            case "Browse1Star": _vm.ShowFilesWithStarTag("1star"); LoadThumbnailsAsync(); e.Handled = true; break;
            case "Browse2Star": _vm.ShowFilesWithStarTag("2star"); LoadThumbnailsAsync(); e.Handled = true; break;
            case "Browse3Star": _vm.ShowFilesWithStarTag("3star"); LoadThumbnailsAsync(); e.Handled = true; break;
            case "Browse4Star": _vm.ShowFilesWithStarTag("4star"); LoadThumbnailsAsync(); e.Handled = true; break;
            case "Browse5Star": _vm.ShowFilesWithStarTag("5star"); LoadThumbnailsAsync(); e.Handled = true; break;
            case "ToggleFind":
                _vm.IsFindPaneOpen = !_vm.IsFindPaneOpen;
                if (_vm.IsFindPaneOpen) FindPathsBox.Focus();
                e.Handled = true; break;
            case "DeleteFiles":
                var sel = FileListView.SelectedItems.OfType<FileItem>().ToList();
                if (sel.Any()) DeleteFiles(sel);
                e.Handled = true; break;
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
        _currentlyPlayingPath = null;
        MediaPlayer.Stop();
        MediaPlayer.Source = null;
        _vm.IsPlaying = false;
        _vm.IsPlayerActive = false;
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
        _currentlyPlayingPath = file.FullPath;
        MediaPlayer.Source = new Uri(file.FullPath);
        MediaPlayer.Play();
        _vm.IsPlaying = true;
        _vm.IsPlayerActive = true;
        _isMediaLoaded = true;
        PlayPauseBtn.Content = "⏸ Pause";
        _timer.Start();
    }

    private void FastForward(int direction = 1)
    {
        if (!_isMediaLoaded || !MediaPlayer.NaturalDuration.HasTimeSpan) return;
        double secs = 10;
        if (double.TryParse(SkipSecondsBox.Text, out var parsed) && parsed > 0) secs = parsed;
        var newPos = MediaPlayer.Position + TimeSpan.FromSeconds(direction * secs);
        var dur = MediaPlayer.NaturalDuration.TimeSpan;
        if (newPos < TimeSpan.Zero) newPos = TimeSpan.Zero;
        if (newPos > dur) newPos = dur;
        MediaPlayer.Position = newPos;
        SeekSlider.Value = newPos.TotalSeconds;
    }

    private void MediaPlayer_MediaOpened(object sender, RoutedEventArgs e)
    {
        _isMediaLoaded = true;
        if (MediaPlayer.NaturalDuration.HasTimeSpan)
            SeekSlider.Maximum = MediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
    }

    private void MediaPlayer_MediaEnded(object sender, RoutedEventArgs e)
    {
        _currentlyPlayingPath = null;
        _vm.IsPlaying = false;
        _vm.IsPlayerActive = false;
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

    private void FastForwardBtn_Click(object sender, RoutedEventArgs e) => FastForward();

    private void MediaPreview_Click(object sender, MouseButtonEventArgs e)
    {
        var path = _currentlyPlayingPath;
        if (path != null) _vm.OpenFileExternal(path);
    }

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
            MessageBox.Show("Please enter a tag name.", "Validation",
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

    // ── Find pane (B) ─────────────────────────────────────────────────────
    private void FindFilesBtn_Click(object sender, RoutedEventArgs e)
        => _vm.FindFilesByPaths(FindPathsBox.Text);

    private void FindPathsBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            _vm.FindFilesByPaths(FindPathsBox.Text);
    }

    private void CloseFindPaneBtn_Click(object sender, RoutedEventArgs e)
        => _vm.IsFindPaneOpen = false;

    private void FindResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var file = FindResultsList.SelectedItem as FileItem;
        if (file == null) return;
        _vm.OpenFileExternal(file.FullPath);
    }

    private void BrowseFilesBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select Files",
            Multiselect = true,
            Filter = "All Files (*.*)|*.*|Media Files|*.mp4;*.mkv;*.avi;*.wmv;*.mov;*.mp3;*.wav;*.flac"
        };
        if (dlg.ShowDialog() != true) return;
        var paths = string.Join("\n", dlg.FileNames);
        FindPathsBox.Text = string.IsNullOrEmpty(FindPathsBox.Text) ? paths : FindPathsBox.Text + "\n" + paths;
    }

    private void BrowseFolderBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Select Folder", Multiselect = true };
        if (dlg.ShowDialog() != true) return;
        var paths = string.Join("\n", dlg.FolderNames);
        FindPathsBox.Text = string.IsNullOrEmpty(FindPathsBox.Text) ? paths : FindPathsBox.Text + "\n" + paths;
    }

    // ── Shortcuts dialog ───────────────────────────────────────────────────
    private void ShowShortcutsBtn_Click(object sender, RoutedEventArgs e)
        => new KeyBindingsDialog(_vm.KeyBindings) { Owner = this }.ShowDialog();
}
