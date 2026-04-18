using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace KoalaFileExplorer.Models;

public class FileItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public long Size { get; set; }
    public DateTime LastModified { get; set; }

    private List<CustomerTag> _tags = new();
    public List<CustomerTag> Tags
    {
        get => _tags;
        set { _tags = value; Notify(); Notify(nameof(HasTags)); Notify(nameof(TagSummary)); }
    }

    private BitmapSource? _thumbnail;
    public BitmapSource? Thumbnail
    {
        get => _thumbnail;
        set { _thumbnail = value; Notify(); Notify(nameof(HasThumbnail)); }
    }

    public bool HasThumbnail => _thumbnail != null;
    public bool HasTags => _tags.Count > 0;
    public string TagSummary => string.Join("  ", _tags.Select(t => t.Name));

    public string SizeDisplay => IsDirectory ? "<DIR>"
        : Size < 1024 ? $"{Size} B"
        : Size < 1_048_576 ? $"{Size / 1024} KB"
        : $"{Size / 1_048_576} MB";

    public string Extension => IsDirectory ? "" : Path.GetExtension(Name).ToLower();
    public bool IsMediaFile => Extension is ".mp4" or ".wmv" or ".avi" or ".mkv"
                                          or ".mov" or ".mp3" or ".wav" or ".flac";
}
