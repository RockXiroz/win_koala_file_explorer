using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KoalaFileExplorer.Models;

public class CustomerTag : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;

    private string _color = "#2196F3";
    public string Color
    {
        get => _color;
        set { _color = value; Notify(); }
    }

    private string _textColor = "#FFFFFF";
    public string TextColor
    {
        get => _textColor;
        set { _textColor = value; Notify(); }
    }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
