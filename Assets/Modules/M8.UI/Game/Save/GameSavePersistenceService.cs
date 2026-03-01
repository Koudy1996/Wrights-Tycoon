using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace WrightTycoon.Game.Save;

public enum SaveSlotType
{
    Manual = 0,
    Autosave = 1,
}

public sealed class GameSaveSlotEntry
{
    public string Id { get; set; } = string.Empty;

    public string Type { get; set; } = "manual";

    public string SlotName { get; set; } = string.Empty;

    public string CreatedAtIso { get; set; } = string.Empty;

    public long CreatedAtUnixMilliseconds { get; set; }

    public int GameMonth { get; set; }

    public string CompanyName { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = string.Empty;
}

public sealed class GameSavePersistenceService
{
    private readonly string _saveFilePath;
    private readonly JsonSerializerSettings _jsonSettings;
    private GameSaveCollection _collection = new();
    private bool _isLoaded;

    public GameSavePersistenceService(string saveFilePath, JsonSerializerSettings? jsonSettings = null)
    {
        if (string.IsNullOrWhiteSpace(saveFilePath))
        {
            throw new ArgumentException("Save file path cannot be empty.", nameof(saveFilePath));
        }

        _saveFilePath = saveFilePath;
        _jsonSettings = jsonSettings ?? CreateDefaultJsonSettings();
    }

    public IReadOnlyList<GameSaveSlotEntry> GetOrderedForLoadMenu()
    {
        EnsureLoaded();

        var autos = _collection.Saves
            .Where(entry => string.Equals(entry.Type, "autosave", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.CreatedAtUnixMilliseconds)
            .ToList();

        var manual = _collection.Saves
            .Where(entry => string.Equals(entry.Type, "manual", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.CreatedAtUnixMilliseconds)
            .ToList();

        var ordered = new List<GameSaveSlotEntry>(autos.Count + manual.Count);
        ordered.AddRange(autos);
        ordered.AddRange(manual);
        return ordered;
    }

    public bool TryGetContinueEntry(out GameSaveSlotEntry? entry)
    {
        EnsureLoaded();

        if (!string.IsNullOrWhiteSpace(_collection.LastPlayedSaveId))
        {
            entry = _collection.Saves.FirstOrDefault(item =>
                string.Equals(item.Id, _collection.LastPlayedSaveId, StringComparison.Ordinal));
            if (entry is not null)
            {
                return true;
            }
        }

        entry = _collection.Saves
            .OrderByDescending(item => item.CreatedAtUnixMilliseconds)
            .FirstOrDefault();
        return entry is not null;
    }

    public GameSaveSlotEntry CreateManualSave(string slotName, int gameMonth, string companyName, string payloadJson)
    {
        return CreateSaveInternal(SaveSlotType.Manual, slotName, gameMonth, companyName, payloadJson);
    }

    public GameSaveSlotEntry CreateAutosave(string slotName, int gameMonth, string companyName, string payloadJson, int autosaveRetentionCount = 4)
    {
        var created = CreateSaveInternal(SaveSlotType.Autosave, slotName, gameMonth, companyName, payloadJson);
        TrimOldestAutosaves(autosaveRetentionCount);
        SaveToDisk();
        return created;
    }

    public bool TryGetById(string id, out GameSaveSlotEntry? entry)
    {
        EnsureLoaded();

        if (string.IsNullOrWhiteSpace(id))
        {
            entry = null;
            return false;
        }

        entry = _collection.Saves.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));
        return entry is not null;
    }

    public bool MarkAsLastPlayed(string id)
    {
        EnsureLoaded();

        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        var entry = _collection.Saves.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));
        if (entry is null)
        {
            return false;
        }

        if (string.Equals(_collection.LastPlayedSaveId, entry.Id, StringComparison.Ordinal))
        {
            return true;
        }

        _collection.LastPlayedSaveId = entry.Id;
        SaveToDisk();
        return true;
    }

    private GameSaveSlotEntry CreateSaveInternal(SaveSlotType type, string slotName, int gameMonth, string companyName, string payloadJson)
    {
        EnsureLoaded();

        var now = DateTimeOffset.Now;
        var entry = new GameSaveSlotEntry
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = type == SaveSlotType.Autosave ? "autosave" : "manual",
            SlotName = string.IsNullOrWhiteSpace(slotName) ? "Unnamed Save" : slotName.Trim(),
            CreatedAtIso = now.ToString("yyyy-MM-dd HH:mm:ss"),
            CreatedAtUnixMilliseconds = now.ToUnixTimeMilliseconds(),
            GameMonth = gameMonth < 0 ? 0 : gameMonth,
            CompanyName = string.IsNullOrWhiteSpace(companyName) ? "Player Company" : companyName.Trim(),
            PayloadJson = payloadJson ?? string.Empty,
        };

        _collection.Saves.Add(entry);
        _collection.LastPlayedSaveId = entry.Id;
        SaveToDisk();
        return entry;
    }

    private void TrimOldestAutosaves(int keepCount)
    {
        if (keepCount < 1)
        {
            keepCount = 1;
        }

        var autos = _collection.Saves
            .Where(entry => string.Equals(entry.Type, "autosave", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.CreatedAtUnixMilliseconds)
            .ToList();

        var removeCount = autos.Count - keepCount;
        if (removeCount <= 0)
        {
            return;
        }

        for (var index = 0; index < removeCount; index++)
        {
            if (string.Equals(_collection.LastPlayedSaveId, autos[index].Id, StringComparison.Ordinal))
            {
                _collection.LastPlayedSaveId = string.Empty;
            }

            _collection.Saves.Remove(autos[index]);
        }
    }

    private void EnsureLoaded()
    {
        if (_isLoaded)
        {
            return;
        }

        LoadFromDisk();
        _isLoaded = true;
    }

    private void LoadFromDisk()
    {
        _collection = new GameSaveCollection();

        if (!File.Exists(_saveFilePath))
        {
            return;
        }

        var json = File.ReadAllText(_saveFilePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        GameSaveCollection? loaded;
        try
        {
            loaded = JsonConvert.DeserializeObject<GameSaveCollection>(json, _jsonSettings);
        }
        catch (JsonException)
        {
            return;
        }

        if (loaded?.Saves is null)
        {
            return;
        }

        _collection = loaded;
    }

    private void SaveToDisk()
    {
        var directory = Path.GetDirectoryName(_saveFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonConvert.SerializeObject(_collection, Formatting.Indented, _jsonSettings);
        File.WriteAllText(_saveFilePath, json);
    }

    private static JsonSerializerSettings CreateDefaultJsonSettings()
    {
        return new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore,
        };
    }

    private sealed class GameSaveCollection
    {
        public string LastPlayedSaveId { get; set; } = string.Empty;

        public List<GameSaveSlotEntry> Saves { get; set; } = new();
    }
}


