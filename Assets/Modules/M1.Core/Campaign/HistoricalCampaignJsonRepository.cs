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

        var json = File.ReadAllText(path);
        return LoadFromJson(json);
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
