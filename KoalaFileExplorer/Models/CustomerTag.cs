namespace KoalaFileExplorer.Models;

public class CustomerTag
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#2196F3";
    public string TextColor { get; set; } = "#FFFFFF";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
