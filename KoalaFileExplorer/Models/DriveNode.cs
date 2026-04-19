using System.Collections.ObjectModel;

namespace KoalaFileExplorer.Models;

public class DriveNode
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsExpanded { get; set; }
    public ObservableCollection<DriveNode> Children { get; set; } = new();
    public bool HasDummyChild => Children.Count == 1 && Children[0].FullPath == "DUMMY";

    // Drive capacity (populated only for root drives)
    public string CapacityText { get; set; } = "";
    public int UsedPercent { get; set; }
    public bool HasCapacity => !string.IsNullOrEmpty(CapacityText);

    public static DriveNode CreateDummy() => new DriveNode { Name = "Loading...", FullPath = "DUMMY" };
}
