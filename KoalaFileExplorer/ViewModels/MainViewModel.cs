using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using KoalaFileExplorer.Models;
using KoalaFileExplorer.Services;

namespace KoalaFileExplorer.ViewModels;

public class MainViewModel : ObservableObject
{
    public readonly TagService _tagService;

    private static readonly string[] _playerPaths =
    {
        @"C:\Program Files\DAUM\PotPlayer\PotPlayerMini64.exe",
        @"C:\Program Files\DAUM\PotPlayer\PotPlayerMini.exe",
        @"C:\Program Files (x86)\DAUM\PotPlayer\PotPlayerMini.exe",
        @"C:\Program Files\VideoLAN\VLC\vlc.exe",
    };

    // ── File Tree ──────────────────────────────────────────────────────────
    public ObservableCollection<DriveNode> RootNodes { get; } = new();

    // ── File List ──────────────────────────────────────────────────────────
    private ObservableCollection<FileItem> _allFiles = new();
    public ObservableCollection<FileItem> Files { get; } = new();

    private FileItem? _selectedFile;
    public FileItem? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (SetField(ref _selectedFile, value))
            {
                OnPropertyChanged(nameof(CanPlayFile));
                OnPropertyChanged(nameof(SelectedFileTags));
                OnPropertyChanged(nameof(SelectedFilePath));
                OnPropertyChanged(nameof(IsNoFileSelected));
                OnPropertyChanged(nameof(IsNonMediaFileSelected));
                OnPropertyChanged(nameof(ShowNoFileSelected));
                MediaUri = null;
            }
        }
    }

    public string SelectedFilePath => SelectedFile?.FullPath ?? string.Empty;
    public bool CanPlayFile => SelectedFile?.IsMediaFile == true;
    public bool IsNoFileSelected => SelectedFile == null;
    public bool IsNonMediaFileSelected => SelectedFile != null && !SelectedFile.IsMediaFile && !SelectedFile.IsDirectory;

    private bool _isPlayerActive;
    public bool IsPlayerActive
    {
        get => _isPlayerActive;
        set { if (SetField(ref _isPlayerActive, value)) OnPropertyChanged(nameof(ShowNoFileSelected)); }
    }

    public bool ShowNoFileSelected => IsNoFileSelected && !IsPlayerActive;

    public List<CustomerTag> SelectedFileTags =>
        SelectedFile == null ? new() : _tagService.GetTagsForFile(SelectedFile.FullPath);

    // ── Media ──────────────────────────────────────────────────────────────
    private Uri? _mediaUri;
    public Uri? MediaUri
    {
        get => _mediaUri;
        set => SetField(ref _mediaUri, value);
    }

    private bool _isPlaying;
    public bool IsPlaying
    {
        get => _isPlaying;
        set => SetField(ref _isPlaying, value);
    }

    // ── Search ────────────────────────────────────────────────────────────
    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set { if (SetField(ref _searchText, value)) ApplyFilter(); }
    }

    private string _searchMode = "Name";
    public string SearchMode
    {
        get => _searchMode;
        set { if (SetField(ref _searchMode, value)) ApplyFilter(); }
    }

    // ── Tags ──────────────────────────────────────────────────────────────
    public ObservableCollection<CustomerTag> AllTags { get; } = new();

    // ── Path ──────────────────────────────────────────────────────────────
    private string _currentPath = string.Empty;
    public string CurrentPath
    {
        get => _currentPath;
        set => SetField(ref _currentPath, value);
    }

    // ── Status ────────────────────────────────────────────────────────────
    private string _statusText = "Ready  |  Keys: 1-5 = star rating  |  0 = clear tags  |  Double-click = open in external player";
    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    // ── Commands ──────────────────────────────────────────────────────────
    public RelayCommand PlayCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand AddTagToFileCommand { get; }
    public RelayCommand RemoveTagFromFileCommand { get; }
    public RelayCommand CreateTagCommand { get; }
    public RelayCommand DeleteTagCommand { get; }
    public RelayCommand SearchByNameCommand { get; }
    public RelayCommand SearchByTagCommand { get; }
    public RelayCommand ClearSearchCommand { get; }
    public RelayCommand LoadChildrenCommand { get; }

    public MainViewModel()
    {
        _tagService = new TagService();

        PlayCommand = new RelayCommand(PlayFile, () => CanPlayFile);
        StopCommand = new RelayCommand(StopMedia);
        AddTagToFileCommand = new RelayCommand(p => AddTagToFilePublic(p as CustomerTag), _ => SelectedFile != null);
        RemoveTagFromFileCommand = new RelayCommand(p => RemoveTagFromFilePublic(p as CustomerTag));
        CreateTagCommand = new RelayCommand(p => CreateTag(p as string));
        DeleteTagCommand = new RelayCommand(p => DeleteTag(p as CustomerTag));
        SearchByNameCommand = new RelayCommand(_ => { SearchMode = "Name"; ApplyFilter(); });
        SearchByTagCommand = new RelayCommand(_ => { SearchMode = "Tag"; ApplyFilter(); });
        ClearSearchCommand = new RelayCommand(() => { SearchText = string.Empty; SearchMode = "Name"; });
        LoadChildrenCommand = new RelayCommand(p => LoadChildren(p as DriveNode));

        LoadDrives();
        RefreshTags();
    }

    // ── File Tree ─────────────────────────────────────────────────────────
    private void LoadDrives()
    {
        RootNodes.Clear();
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
        {
            var node = new DriveNode
            {
                Name = $"{drive.Name} ({drive.VolumeLabel})",
                FullPath = drive.RootDirectory.FullName
            };
            node.Children.Add(DriveNode.CreateDummy());
            RootNodes.Add(node);
        }
    }

    public void LoadChildren(DriveNode? node)
    {
        if (node == null || !node.HasDummyChild) return;
        node.Children.Clear();
        try
        {
            foreach (var dir in Directory.GetDirectories(node.FullPath)
                .Select(d => new DirectoryInfo(d))
                .Where(d => !d.Attributes.HasFlag(FileAttributes.Hidden | FileAttributes.System))
                .OrderBy(d => d.Name))
            {
                var child = new DriveNode { Name = dir.Name, FullPath = dir.FullName };
                try { if (Directory.GetDirectories(dir.FullName).Length > 0) child.Children.Add(DriveNode.CreateDummy()); } catch { }
                node.Children.Add(child);
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
    }

    public void NavigateTo(string path)
    {
        if (!Directory.Exists(path)) return;
        CurrentPath = path;
        LoadFiles(path);
    }

    private void LoadFiles(string path)
    {
        _allFiles.Clear();
        Files.Clear();
        SearchText = string.Empty;
        try
        {
            var dirs = Directory.GetDirectories(path)
                .Select(d => new DirectoryInfo(d))
                .Where(d => !d.Attributes.HasFlag(FileAttributes.Hidden))
                .OrderBy(d => d.Name)
                .Select(d => new FileItem { Name = d.Name, FullPath = d.FullName, IsDirectory = true, LastModified = d.LastWriteTime });

            var files = Directory.GetFiles(path)
                .Select(f => new FileInfo(f))
                .Where(f => !f.Attributes.HasFlag(FileAttributes.Hidden))
                .OrderBy(f => f.Name)
                .Select(f => new FileItem
                {
                    Name = f.Name, FullPath = f.FullName, IsDirectory = false,
                    Size = f.Length, LastModified = f.LastWriteTime,
                    Tags = _tagService.GetTagsForFile(f.FullName)
                });

            foreach (var item in dirs.Concat(files))
                _allFiles.Add(item);

            ApplyFilter();
            StatusText = $"{_allFiles.Count} items  |  Keys: 1-5 = star rating  |  0 = clear tags  |  Double-click = external player";
        }
        catch (UnauthorizedAccessException) { StatusText = "Access denied."; }
    }

    private void ApplyFilter()
    {
        Files.Clear();
        var text = SearchText.Trim();
        IEnumerable<FileItem> results = _allFiles;

        if (!string.IsNullOrEmpty(text))
        {
            if (SearchMode == "Tag")
            {
                var tagged = new HashSet<string>(_tagService.GetFilesByTagName(text), StringComparer.OrdinalIgnoreCase);
                results = results.Where(f => tagged.Contains(f.FullPath));
            }
            else
            {
                results = results.Where(f => f.Name.Contains(text, StringComparison.OrdinalIgnoreCase));
            }
        }

        foreach (var item in results)
            Files.Add(item);
    }

    // ── Show All Files By Tag ─────────────────────────────────────────────
    public void ShowFilesWithTag(CustomerTag tag)
    {
        var paths = _tagService.GetFilesByTag(tag.Id);
        _allFiles.Clear();
        Files.Clear();
        SearchText = string.Empty;
        CurrentPath = $"📌 Tag: {tag.Name}";

        foreach (var path in paths)
        {
            if (!File.Exists(path)) continue;
            var fi = new FileInfo(path);
            var item = new FileItem
            {
                Name = fi.Name, FullPath = fi.FullName, IsDirectory = false,
                Size = fi.Length, LastModified = fi.LastWriteTime,
                Tags = _tagService.GetTagsForFile(path)
            };
            _allFiles.Add(item);
            Files.Add(item);
        }
        StatusText = $"Found {Files.Count} files tagged '{tag.Name}'";
    }

    // ── Media ─────────────────────────────────────────────────────────────
    private void PlayFile()
    {
        if (SelectedFile?.IsMediaFile == true)
        {
            MediaUri = new Uri(SelectedFile.FullPath);
            IsPlaying = true;
        }
    }

    private void StopMedia() { MediaUri = null; IsPlaying = false; }

    // ── Tags ──────────────────────────────────────────────────────────────
    private void RefreshTags()
    {
        AllTags.Clear();
        foreach (var t in _tagService.GetAllTags())
            AllTags.Add(t);
    }

    public void CreateTag(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        try { _tagService.CreateTag(name); RefreshTags(); StatusText = $"Tag '{name}' created."; }
        catch (Exception ex) { StatusText = $"Error: {ex.Message}"; }
    }

    public void DeleteTag(CustomerTag? tag)
    {
        if (tag == null) return;
        _tagService.DeleteTag(tag.Id);
        RefreshTags();
        if (SelectedFile != null) OnPropertyChanged(nameof(SelectedFileTags));
        StatusText = $"Tag '{tag.Name}' deleted.";
    }

    public void AddTagToFilePublic(CustomerTag? tag)
    {
        if (tag == null || SelectedFile == null) return;
        var file = SelectedFile;
        _tagService.AddTagToFile(file.FullPath, tag.Id);
        file.Tags = _tagService.GetTagsForFile(file.FullPath);
        OnPropertyChanged(nameof(SelectedFileTags));
        StatusText = $"Tagged '{file.Name}' with '{tag.Name}'.";
    }

    public void RemoveTagFromFilePublic(CustomerTag? tag)
    {
        if (tag == null || SelectedFile == null) return;
        var file = SelectedFile;
        _tagService.RemoveTagFromFile(file.FullPath, tag.Id);
        file.Tags = _tagService.GetTagsForFile(file.FullPath);
        OnPropertyChanged(nameof(SelectedFileTags));
        StatusText = $"Removed tag '{tag.Name}' from '{file.Name}'.";
    }

    // ── Shortcut helpers ──────────────────────────────────────────────────
    public void AddStarTag(string tagName)
    {
        if (SelectedFile == null) return;
        var tag = _tagService.GetAllTags().FirstOrDefault(t => t.Name == tagName);
        if (tag != null) AddTagToFilePublic(tag);
    }

    public void RemoveAllTagsFromFile()
    {
        if (SelectedFile == null) return;
        var file = SelectedFile;
        _tagService.RemoveAllTagsFromFile(file.FullPath);
        file.Tags = new List<CustomerTag>();
        OnPropertyChanged(nameof(SelectedFileTags));
        StatusText = $"Removed all tags from '{file.Name}'.";
    }

    // ── Open External ─────────────────────────────────────────────────────
    public void OpenFileExternal(string filePath)
    {
        var player = _playerPaths.FirstOrDefault(File.Exists);
        try
        {
            if (player != null)
                Process.Start(player, $"\"{filePath}\"");
            else
                Process.Start(new ProcessStartInfo { FileName = filePath, UseShellExecute = true });
            StatusText = $"Opened: {Path.GetFileName(filePath)}";
        }
        catch (Exception ex) { StatusText = $"Error opening file: {ex.Message}"; }
    }
}
