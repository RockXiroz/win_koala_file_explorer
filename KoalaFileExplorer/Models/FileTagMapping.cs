namespace KoalaFileExplorer.Models;

public class FileTagMapping
{
    public string FilePath { get; set; } = string.Empty;
    public List<string> TagIds { get; set; } = new();
}
