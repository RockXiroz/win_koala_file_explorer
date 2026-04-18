using System.IO;

namespace KoalaFileExplorer.Models;

public class FileItem
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
    public List<CustomerTag> Tags { get; set; } = new();

    public string SizeDisplay => IsDirectory
        ? "<DIR>"
        : Size < 1024 ? $"{Size} B"
        : Size < 1024 * 1024 ? $"{Size / 1024} KB"
        : $"{Size / (1024 * 1024)} MB";

    public string Extension => IsDirectory ? "" : Path.GetExtension(Name).ToLower();
    public bool IsMediaFile => Extension is ".mp4" or ".wmv" or ".avi" or ".mkv" or ".mov" or ".mp3" or ".wav" or ".flac";
}
