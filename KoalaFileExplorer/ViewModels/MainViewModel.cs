using System.Collections.ObjectModel;
using System.IO;
using KoalaFileExplorer.Models;
using KoalaFileExplorer.Services;

namespace KoalaFileExplorer.ViewModels;

public class MainViewModel : ObservableObject
{
    public readonly TagService _tagService;

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
                MediaUri = null;
            }
        }
    }

    public string SelectedFilePath => SelectedFile?.FullPath ?? string.Empty;
    public bool CanPlayFile => SelectedFile?.IsMediaFile == true;
    public List<CustomerTag> SelectedFileTags =>
        SelectedFile == null ? new() : _tagService.GetTagsForFile(SelectedFile.FullPath);

    // ── Media Player ──────────────────────────────────────────────────────
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
        set
        {
            if (SetField(ref _searchText, value))
                ApplyFilter();
        }
    }

    private string _searchMode = "Name";
    public string SearchMode
    {
        get => _searchMode;
        set
        {
            if (SetField(ref _searchMode, value))
                ApplyFilter();
        }
    }

    // ── Tags ──────────────────────────────────────────────────────────────
    public ObservableCollection<CustomerTag> AllTags { get; } = new();

    private CustomerTag? _selectedTagFilter;
    public CustomerTag? SelectedTagFilter
    {
        get => _selectedTagFilter;
        set => SetField(ref _selectedTagFilter, value);
    }

    // ── Current path ──────────────────────────────────────────────────────
    private string _currentPath = string.Empty;
    public string CurrentPath
    {
        get => _currentPath;
        set => SetField(ref _currentPath, value);
    }

    // ── Status ────────────────────────────────────────────────────────────
    private string _statusText = "Ready";
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
        AddTagToFileCommand = new RelayCommand(p => AddTagToFile(p as CustomerTag), _ => SelectedFile != null);
        RemoveTagFromFileCommand = new RelayCommand(p => RemoveTagFromFile(p as CustomerTag));
        CreateTagCommand = new RelayCommand(p => CreateTag(p as string));
        DeleteTagCommand = new RelayCommand(p => DeleteTag(p as CustomerTag));
        SearchByNameCommand = new RelayCommand(_ => { SearchMode = "Name"; ApplyFilter(); });
        SearchByTagCommand = new RelayCommand(_ => { SearchMode = "Tag"; ApplyFilter(); });
        ClearSearchCommand = new RelayCommand(() => { SearchText = string.Empty; SearchMode = "Name"; });
        LoadChildrenCommand = new RelayCommand(p => LoadChildren(p as DriveNode));

        LoadDrives();
        RefreshTags();
    }

    // ── File Tree Loading ─────────────────────────────────────────────────
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
            var dirs = Directory.GetDirectories(node.FullPath)
                .Select(d => new DirectoryInfo(d))
                .Where(d => !d.Attributes.HasFlag(FileAttributes.Hidden | FileAttributes.System))
                .OrderBy(d => d.Name);

            foreach (var dir in dirs)
            {
                var child = new DriveNode { Name = dir.Name, FullPath = dir.FullName };
                try
                {
                    if (Directory.GetDirectories(dir.FullName).Length > 0)
                        child.Children.Add(DriveNode.CreateDummy());
                }
                catch { }
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
                .Select(d => new FileItem
                {
                    Name = d.Name,
                    FullPath = d.FullName,
                    IsDirectory = true,
                    LastModified = d.LastWriteTime
                });

            var files = Directory.GetFiles(path)
                .Select(f => new FileInfo(f))
                .Where(f => !f.Attributes.HasFlag(FileAttributes.Hidden))
                .OrderBy(f => f.Name)
                .Select(f => new FileItem
                {
                    Name = f.Name,
                    FullPath = f.FullName,
                    IsDirectory = false,
                    Size = f.Length,
                    LastModified = f.LastWriteTime,
                    Tags = _tagService.GetTagsForFile(f.FullName)
                });

            foreach (var item in dirs.Concat(files))
                _allFiles.Add(item);

            ApplyFilter();
            StatusText = $"{_allFiles.Count} items in {path}";
        }
        catch (UnauthorizedAccessException)
        {
            StatusText = "Access denied.";
        }
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
                var taggedPaths = new HashSet<string>(
                    _tagService.GetFilesByTagName(text),
                    StringComparer.OrdinalIgnoreCase);
                results = results.Where(f => taggedPaths.Contains(f.FullPath));
            }
            else
            {
                results = results.Where(f =>
                    f.Name.Contains(text, StringComparison.OrdinalIgnoreCase));
            }
        }

        foreach (var item in results)
            Files.Add(item);
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

    private void StopMedia()
    {
        MediaUri = null;
        IsPlaying = false;
    }

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
        try
        {
            _tagService.CreateTag(name);
            RefreshTags();
            StatusText = $"Tag '{name}' created.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }

    public void DeleteTag(CustomerTag? tag)
    {
        if (tag == null) return;
        _tagService.DeleteTag(tag.Id);
        RefreshTags();
        if (SelectedFile != null)
            OnPropertyChanged(nameof(SelectedFileTags));
        StatusText = $"Tag '{tag.Name}' deleted.";
    }

    private void AddTagToFile(CustomerTag? tag) => AddTagToFilePublic(tag);

    private void RemoveTagFromFile(CustomerTag? tag) => RemoveTagFromFilePublic(tag);

    public void AddTagToFilePublic(CustomerTag? tag)
    {
        if (tag == null || SelectedFile == null) return;
        _tagService.AddTagToFile(SelectedFile.FullPath, tag.Id);
        SelectedFile.Tags = _tagService.GetTagsForFile(SelectedFile.FullPath);
        OnPropertyChanged(nameof(SelectedFileTags));
        RefreshFileList();
        StatusText = $"Tagged '{SelectedFile.Name}' with '{tag.Name}'.";
    }

    public void RemoveTagFromFilePublic(CustomerTag? tag)
    {
        if (tag == null || SelectedFile == null) return;
        _tagService.RemoveTagFromFile(SelectedFile.FullPath, tag.Id);
        SelectedFile.Tags = _tagService.GetTagsForFile(SelectedFile.FullPath);
        OnPropertyChanged(nameof(SelectedFileTags));
        RefreshFileList();
        StatusText = $"Removed tag '{tag.Name}' from '{SelectedFile.Name}'.";
    }

    private void RefreshFileList()
    {
        // Re-apply filter to refresh tag display in file list
        ApplyFilter();
    }
}
