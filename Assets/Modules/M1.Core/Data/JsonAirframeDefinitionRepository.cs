using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using WrightTycoon.Core.Airframes;

namespace WrightTycoon.Core.Data;

public sealed class JsonAirframeDefinitionRepository
{
    private readonly JsonSerializerSettings _serializerSettings;

    public JsonAirframeDefinitionRepository(JsonSerializerSettings? serializerSettings = null)
    {
        _serializerSettings = serializerSettings ?? CreateDefaultJsonSettings();
    }

    public AirframeDefinitionRepository LoadFromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("JSON path cannot be empty.", nameof(path));
        }

        var json = File.ReadAllText(path);
        return LoadFromJson(json);
    }

    public AirframeDefinitionRepository LoadFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("JSON payload cannot be empty.", nameof(json));
        }

        var envelope = JsonConvert.DeserializeObject<AirframeDefinitionEnvelope>(json, _serializerSettings);
        if (envelope?.Definitions is null || envelope.Definitions.Count == 0)
        {
            throw new InvalidOperationException("Airframe definitions JSON did not contain any entries.");
        }

        return new AirframeDefinitionRepository(envelope.Definitions);
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

    private sealed class AirframeDefinitionEnvelope
    {
        public List<AirframeDefinition> Definitions { get; init; } = new();
    }
}
