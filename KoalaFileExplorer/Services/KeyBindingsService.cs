using System.IO;
using System.Text.Json;
using System.Windows.Input;
using KoalaFileExplorer.Models;

namespace KoalaFileExplorer.Services;

public class KeyBindingsService
{
    private readonly string _file;
    private static readonly JsonSerializerOptions _opts = new() { WriteIndented = true };

    private static readonly (string action, string desc, Key key, ModifierKeys mods)[] _defaults =
    {
        ("ClearTags",   "Clear all tags from selected file",  Key.D0,     ModifierKeys.None),
        ("Tag1Star",    "Tag selected file with 1star",        Key.D1,     ModifierKeys.None),
        ("Tag2Star",    "Tag selected file with 2star",        Key.D2,     ModifierKeys.None),
        ("Tag3Star",    "Tag selected file with 3star",        Key.D3,     ModifierKeys.None),
        ("Tag4Star",    "Tag selected file with 4star",        Key.D4,     ModifierKeys.None),
        ("Tag5Star",    "Tag selected file with 5star",        Key.D5,     ModifierKeys.None),
        ("FastForward", "Fast-forward playback",               Key.Right,  ModifierKeys.None),
        ("Rewind",      "Rewind playback",                     Key.Left,   ModifierKeys.None),
        ("Browse1Star", "Browse all 1star-tagged files",       Key.D1,     ModifierKeys.Control),
        ("Browse2Star", "Browse all 2star-tagged files",       Key.D2,     ModifierKeys.Control),
        ("Browse3Star", "Browse all 3star-tagged files",       Key.D3,     ModifierKeys.Control),
        ("Browse4Star", "Browse all 4star-tagged files",       Key.D4,     ModifierKeys.Control),
        ("Browse5Star", "Browse all 5star-tagged files",       Key.D5,     ModifierKeys.Control),
        ("ToggleFind",  "Toggle find-files pane",              Key.F,      ModifierKeys.Control),
        ("DeleteFiles", "Delete selected file(s)",             Key.Delete, ModifierKeys.None),
    };

    public List<KeyBindingEntry> Bindings { get; private set; } = new();

    public KeyBindingsService(string dataDir)
    {
        _file = Path.Combine(dataDir, "keybindings.json");
        LoadWithDefaults();
    }

    private void LoadWithDefaults()
    {
        Bindings = _defaults.Select(d => new KeyBindingEntry
        {
            Action = d.action, Description = d.desc,
            KeyCode = (int)d.key, ModifierCode = (int)d.mods
        }).ToList();

        try
        {
            if (!File.Exists(_file)) return;
            var saved = JsonSerializer.Deserialize<List<KeyBindingEntry>>(File.ReadAllText(_file), _opts);
            if (saved == null) return;
            foreach (var s in saved)
            {
                var e = Bindings.FirstOrDefault(b => b.Action == s.Action);
                if (e != null) { e.KeyCode = s.KeyCode; e.ModifierCode = s.ModifierCode; }
            }
        }
        catch { }
    }

    public string? GetAction(Key key, ModifierKeys mods)
        => Bindings.FirstOrDefault(b => b.KeyCode == (int)key && b.ModifierCode == (int)mods)?.Action;

    public void Save()
        => File.WriteAllText(_file, JsonSerializer.Serialize(Bindings, _opts));

    public void ResetDefaults()
    {
        foreach (var d in _defaults)
        {
            var e = Bindings.FirstOrDefault(b => b.Action == d.action);
            if (e != null) { e.KeyCode = (int)d.key; e.ModifierCode = (int)d.mods; }
        }
        Save();
    }
}
