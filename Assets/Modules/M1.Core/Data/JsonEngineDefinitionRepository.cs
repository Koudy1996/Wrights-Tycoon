using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using WrightTycoon.Core.Engines;

namespace WrightTycoon.Core.Data;

public sealed class JsonEngineDefinitionRepository
{
    private readonly JsonSerializerSettings _serializerSettings;

    public JsonEngineDefinitionRepository(JsonSerializerSettings? serializerSettings = null)
    {
        _serializerSettings = serializerSettings ?? CreateDefaultJsonSettings();
    }

    public EngineDefinitionRepository LoadFromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("JSON path cannot be empty.", nameof(path));
        }

        var json = File.ReadAllText(path);
        return LoadFromJson(json);
    }

    public EngineDefinitionRepository LoadFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("JSON payload cannot be empty.", nameof(json));
        }

        var envelope = JsonConvert.DeserializeObject<EngineDefinitionEnvelope>(json, _serializerSettings);
        if (envelope?.Definitions is null || envelope.Definitions.Count == 0)
        {
            throw new InvalidOperationException("Engine definitions JSON did not contain any entries.");
        }

        return new EngineDefinitionRepository(envelope.Definitions);
    }

    private static JsonSerializerSettings CreateDefaultJsonSettings()
    {
        var settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
        };

        settings.Converters.Add(new StringEnumConverter());
        return settings;
    }

    private sealed class EngineDefinitionEnvelope
    {
        public List<EngineDefinition> Definitions { get; init; } = new();
    }
}
