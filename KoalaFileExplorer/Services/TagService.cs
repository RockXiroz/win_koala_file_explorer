using System.IO;
using System.Text.Json;
using KoalaFileExplorer.Models;

namespace KoalaFileExplorer.Services;

public class TagService
{
    private readonly string _dataDir;
    private readonly string _tagsFile;
    private readonly string _mappingsFile;

    private List<CustomerTag> _tags = new();
    private List<FileTagMapping> _mappings = new();

    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    private static readonly (string id, string name, string color)[] _builtinTags =
    {
        ("builtin-1star", "1star", "#9E9E9E"),
        ("builtin-2star", "2star", "#64B5F6"),
        ("builtin-3star", "3star", "#81C784"),
        ("builtin-4star", "4star", "#FFB74D"),
        ("builtin-5star", "5star", "#E57373"),
    };

    public TagService()
    {
        _dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KoalaFileExplorer");
        _tagsFile = Path.Combine(_dataDir, "tags.json");
        _mappingsFile = Path.Combine(_dataDir, "mappings.json");
        Load();
        EnsureBuiltinTags();
    }

    private void EnsureBuiltinTags()
    {
        bool changed = false;
        // Insert in reverse so they appear in order 1-5 at the top
        foreach (var (id, name, color) in _builtinTags.Reverse())
        {
            if (!_tags.Any(t => t.Id == id))
            {
                _tags.Insert(0, new CustomerTag { Id = id, Name = name, Color = color });
                changed = true;
            }
        }
        if (changed) Save();
    }

    public IReadOnlyList<CustomerTag> GetAllTags() => _tags.AsReadOnly();

    public CustomerTag? GetTag(string id) => _tags.FirstOrDefault(t => t.Id == id);

    public CustomerTag CreateTag(string name, string color = "#2196F3", string textColor = "#FFFFFF")
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tag name cannot be empty.");
        if (_tags.Any(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Tag '{name}' already exists.");
        var tag = new CustomerTag { Name = name.Trim(), Color = color, TextColor = textColor };
        _tags.Add(tag);
        Save();
        return tag;
    }

    public void DeleteTag(string tagId)
    {
        _tags.RemoveAll(t => t.Id == tagId);
        foreach (var m in _mappings) m.TagIds.Remove(tagId);
        _mappings.RemoveAll(m => m.TagIds.Count == 0);
        Save();
    }

    public List<CustomerTag> GetTagsForFile(string filePath)
    {
        var mapping = _mappings.FirstOrDefault(m =>
            m.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
        if (mapping == null) return new();
        return mapping.TagIds
            .Select(id => _tags.FirstOrDefault(t => t.Id == id))
            .Where(t => t != null).Select(t => t!).ToList();
    }

    public void AddTagToFile(string filePath, string tagId)
    {
        var mapping = _mappings.FirstOrDefault(m =>
            m.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
        if (mapping == null) { mapping = new FileTagMapping { FilePath = filePath }; _mappings.Add(mapping); }
        if (!mapping.TagIds.Contains(tagId)) { mapping.TagIds.Add(tagId); Save(); }
    }

    public void RemoveTagFromFile(string filePath, string tagId)
    {
        var mapping = _mappings.FirstOrDefault(m =>
            m.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
        if (mapping == null) return;
        mapping.TagIds.Remove(tagId);
        if (mapping.TagIds.Count == 0) _mappings.Remove(mapping);
        Save();
    }

    public void RemoveAllTagsFromFile(string filePath)
    {
        _mappings.RemoveAll(m => m.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
        Save();
    }

    public List<string> GetFilesByTag(string tagId)
        => _mappings.Where(m => m.TagIds.Contains(tagId)).Select(m => m.FilePath).ToList();

    public List<string> GetFilesByTagName(string tagName)
    {
        var tag = _tags.FirstOrDefault(t => t.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase));
        return tag == null ? new() : GetFilesByTag(tag.Id);
    }

    private void Load()
    {
        Directory.CreateDirectory(_dataDir);
        try { if (File.Exists(_tagsFile)) _tags = JsonSerializer.Deserialize<List<CustomerTag>>(File.ReadAllText(_tagsFile)) ?? new(); } catch { _tags = new(); }
        try { if (File.Exists(_mappingsFile)) _mappings = JsonSerializer.Deserialize<List<FileTagMapping>>(File.ReadAllText(_mappingsFile)) ?? new(); } catch { _mappings = new(); }
    }

    private void Save()
    {
        File.WriteAllText(_tagsFile, JsonSerializer.Serialize(_tags, _jsonOptions));
        File.WriteAllText(_mappingsFile, JsonSerializer.Serialize(_mappings, _jsonOptions));
    }
}
