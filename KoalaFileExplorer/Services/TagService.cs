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

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public TagService()
    {
        _dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KoalaFileExplorer");
        _tagsFile = Path.Combine(_dataDir, "tags.json");
        _mappingsFile = Path.Combine(_dataDir, "mappings.json");
        Load();
    }

    public IReadOnlyList<CustomerTag> GetAllTags() => _tags.AsReadOnly();

    public CustomerTag? GetTag(string id) => _tags.FirstOrDefault(t => t.Id == id);

    public CustomerTag CreateTag(string name, string color = "#2196F3")
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tag name cannot be empty.");
        if (_tags.Any(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Tag '{name}' already exists.");

        var tag = new CustomerTag { Name = name.Trim(), Color = color };
        _tags.Add(tag);
        Save();
        return tag;
    }

    public void DeleteTag(string tagId)
    {
        _tags.RemoveAll(t => t.Id == tagId);
        foreach (var m in _mappings)
            m.TagIds.Remove(tagId);
        _mappings.RemoveAll(m => m.TagIds.Count == 0);
        Save();
    }

    public List<CustomerTag> GetTagsForFile(string filePath)
    {
        var mapping = _mappings.FirstOrDefault(m =>
            m.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
        if (mapping == null) return new List<CustomerTag>();

        return mapping.TagIds
            .Select(id => _tags.FirstOrDefault(t => t.Id == id))
            .Where(t => t != null)
            .Select(t => t!)
            .ToList();
    }

    public void AddTagToFile(string filePath, string tagId)
    {
        var mapping = _mappings.FirstOrDefault(m =>
            m.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));

        if (mapping == null)
        {
            mapping = new FileTagMapping { FilePath = filePath };
            _mappings.Add(mapping);
        }

        if (!mapping.TagIds.Contains(tagId))
        {
            mapping.TagIds.Add(tagId);
            Save();
        }
    }

    public void RemoveTagFromFile(string filePath, string tagId)
    {
        var mapping = _mappings.FirstOrDefault(m =>
            m.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
        if (mapping == null) return;

        mapping.TagIds.Remove(tagId);
        if (mapping.TagIds.Count == 0)
            _mappings.Remove(mapping);
        Save();
    }

    public List<string> GetFilesByTag(string tagId)
    {
        return _mappings
            .Where(m => m.TagIds.Contains(tagId))
            .Select(m => m.FilePath)
            .ToList();
    }

    public List<string> GetFilesByTagName(string tagName)
    {
        var tag = _tags.FirstOrDefault(t =>
            t.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase));
        if (tag == null) return new List<string>();
        return GetFilesByTag(tag.Id);
    }

    private void Load()
    {
        Directory.CreateDirectory(_dataDir);
        try
        {
            if (File.Exists(_tagsFile))
                _tags = JsonSerializer.Deserialize<List<CustomerTag>>(File.ReadAllText(_tagsFile)) ?? new();
        }
        catch { _tags = new(); }

        try
        {
            if (File.Exists(_mappingsFile))
                _mappings = JsonSerializer.Deserialize<List<FileTagMapping>>(File.ReadAllText(_mappingsFile)) ?? new();
        }
        catch { _mappings = new(); }
    }

    private void Save()
    {
        File.WriteAllText(_tagsFile, JsonSerializer.Serialize(_tags, _jsonOptions));
        File.WriteAllText(_mappingsFile, JsonSerializer.Serialize(_mappings, _jsonOptions));
    }
}
