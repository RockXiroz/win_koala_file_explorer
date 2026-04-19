using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KoalaFileExplorer.Models;

public class KeyBindingEntry : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    public string Action { get; set; } = "";
    public string Description { get; set; } = "";

    private int _keyCode;
    public int KeyCode
    {
        get => _keyCode;
        set { _keyCode = value; Notify(); Notify(nameof(DisplayText)); }
    }

    private int _modifierCode;
    public int ModifierCode
    {
        get => _modifierCode;
        set { _modifierCode = value; Notify(); Notify(nameof(DisplayText)); }
    }

    public string DisplayText
    {
        get
        {
            var key = (System.Windows.Input.Key)_keyCode;
            var mods = (System.Windows.Input.ModifierKeys)_modifierCode;
            var keyStr = key switch
            {
                System.Windows.Input.Key.D0 => "0",
                System.Windows.Input.Key.D1 => "1",
                System.Windows.Input.Key.D2 => "2",
                System.Windows.Input.Key.D3 => "3",
                System.Windows.Input.Key.D4 => "4",
                System.Windows.Input.Key.D5 => "5",
                System.Windows.Input.Key.Delete => "Del",
                _ => key.ToString()
            };
            var parts = new List<string>();
            if (mods.HasFlag(System.Windows.Input.ModifierKeys.Control)) parts.Add("Ctrl");
            if (mods.HasFlag(System.Windows.Input.ModifierKeys.Shift)) parts.Add("Shift");
            if (mods.HasFlag(System.Windows.Input.ModifierKeys.Alt)) parts.Add("Alt");
            parts.Add(keyStr);
            return string.Join("+", parts);
        }
    }
}
