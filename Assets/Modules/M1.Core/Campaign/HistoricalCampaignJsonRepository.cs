using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace WrightTycoon.Core.Campaign;

public sealed class HistoricalCampaignJsonRepository
{
    private readonly JsonSerializerSettings _serializerSettings;

    public HistoricalCampaignJsonRepository()
    {
        _serializerSettings = CreateDefaultJsonSettings();
    }

    public HistoricalCampaignService LoadFromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("JSON path cannot be empty.", nameof(path));
        }

        try
        {
            var json = File.ReadAllText(path);
            return LoadFromJson(json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or JsonException)
        {
            throw new InvalidOperationException(
                $"Failed to load historical campaign from '{path}': {ex.Message}",
                ex);
        }
    }

    public HistoricalCampaignService LoadFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("JSON payload cannot be empty.", nameof(json));
        }

        var envelope = JsonConvert.DeserializeObject<HistoricalCampaignEnvelope>(json, _serializerSettings);
        if (envelope?.Events is null || envelope.Events.Count == 0)
        {
            throw new InvalidOperationException("Historical campaign JSON did not contain any events.");
        }

        return new HistoricalCampaignService(envelope.Events);
    }

    public bool TryLoadFromFile(string path, out HistoricalCampaignService? service, out string errorMessage)
    {
        service = null;
        errorMessage = string.Empty;
        try
        {
            service = LoadFromFile(path);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private static JsonSerializerSettings CreateDefaultJsonSettings() =>
        new()
        {
            Formatting = Formatting.Indented,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
        };

    private sealed class HistoricalCampaignEnvelope
    {
        public List<HistoricalCampaignEventDefinition> Events { get; init; } = new();
    }
}
