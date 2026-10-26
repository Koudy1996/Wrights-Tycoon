using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WrightTycoon.Core.Fleet;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Factories;
using WrightTycoon.Core.Contracts;
using WrightTycoon.Core.Engines;
using WrightTycoon.Core.Research;

namespace WrightTycoon.Game.Launch;

public sealed partial class WrightTycoonLaunchRunner
{
    private SparePartsGroupRegistry _enginePartsGroups = new();
    private SparePartsGroupRegistry _airframePartsGroups = new();
    private bool _showEngineSimilarity;

    private string EngineFamilyRoot(string id)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (visited.Add(id))
        {
            var saved = _savedEngines.FirstOrDefault(e => e.ResearchId == id);
            if (saved is null || string.IsNullOrWhiteSpace(saved.SourceResearchId)) return id;
            id = saved.SourceResearchId;
        }
        return visited.OrderBy(v => v, StringComparer.Ordinal).First();
    }

    private void RefreshPartsGroups()
    {
        foreach (var saved in _savedEngines.Where(e => e.ApprovedYear.HasValue
            || (_researchBoard?.IsCompleted(ResearchSubjectType.Engine, e.ResearchId) ?? false)))
        {
            if (_enginePartsGroups.GetGroupId(saved.ResearchId) is not null) continue;
            _enginePartsGroups.Resolve(saved.ResearchId, EngineFamilyRoot(saved.ResearchId), (a, b) =>
            {
                var first = _savedEngines.FirstOrDefault(e => e.ResearchId == a);
                var second = _savedEngines.FirstOrDefault(e => e.ResearchId == b);
                return first is null || second is null ? 100d : MachineSimilarityCalculator.EngineDifference(
                    BuildSavedEngineAtReferenceDate(first), BuildSavedEngineAtReferenceDate(second)).DifferencePercent;
            });
        }
        _airframePartsGroups.MergeFamilyGroups();
        foreach (var saved in _savedAircraftDesigns.Where(IsAircraftDesignReadyForProduction))
        {
            _airframePartsGroups.ResolveFamily(saved.CatalogItem.DesignId, saved.FamilyResearchId);
        }
        _productionFactory?.SetAirframePartsGroups(_airframePartsGroups.CreateSnapshot());
        foreach (var target in _airframeSparePartStockTargets.ToArray())
        {
            var group = _airframePartsGroups.GetGroupId(target.Key) ?? target.Key;
            if (group == target.Key) continue;
            _airframeSparePartStockTargets.TryGetValue(group, out var existing);
            _airframeSparePartStockTargets[group] = (int)Math.Min(int.MaxValue, (long)existing + target.Value);
            _airframeSparePartStockTargets.Remove(target.Key);
        }
    }

    private ServiceOfferSnapshot BuildCombinedMaintenanceOffer(SavedAircraftDesign saved, double? reliability = null,
        WrightTycoon.Core.Aircraft.Aircraft? preview = null)
    {
        RefreshPartsGroups();
        var id = saved.CatalogItem.DesignId;
        var aircraftGroup = _airframePartsGroups.GetGroupId(id) ?? id;
        var engineGroup = _enginePartsGroups.GetGroupId(saved.EngineResearchId) ?? saved.EngineResearchId;
        var aircraft = preview ?? saved.CatalogItem.Aircraft;
        var offer = LifecycleCostCalculator.CreateEngineLifecycleServiceOffer(aircraft,
            reliability ?? ResolveEffectiveAircraftReliability(aircraft.Performance.OverallReliability),
            saved.EngineResearchId, aircraftGroup, engineGroup);
        return RepriceMaintenanceOffer(offer);
    }

    private Engine BuildReferenceEngine(SavedEngineDesign saved) =>
        BuildSavedEngineWithResearch(saved, new TypeResearchModifierPoints());

    private WrightTycoon.Core.Aircraft.Aircraft BuildReferenceAircraft(SavedAircraftDesign saved)
    {
        if (_aircraftFactory is null) throw new InvalidOperationException("Aircraft factory is not initialized.");
        var family = ResolveFamilyDefinitionWithSavedEngineTechnology(saved)
            .WithResearchModifiers(new TypeResearchModifierPoints())
            .WithInstalledEngineResearchModifiers(new TypeResearchModifierPoints());
        var source = saved.CatalogItem.Aircraft.TypeDefinition;
        var type = new AircraftTypeDefinition
        {
            Id = source.Id, FamilyId = source.FamilyId, DisplayName = source.DisplayName,
            EngineDelta = source.EngineDelta, AirframeDelta = source.AirframeDelta,
            NavigationEquipment = source.NavigationEquipment, HasPassengerCabin = source.HasPassengerCabin,
            HasCargoSpace = source.HasCargoSpace, WeaponPayloadKilograms = source.WeaponPayloadKilograms,
            HasSpecialEquipment = source.HasSpecialEquipment, HasCombatEquipment = source.HasCombatEquipment,
            ConstructionDurabilityLevel = source.ConstructionDurabilityLevel, ServiceIntervalHours = source.ServiceIntervalHours,
            GuaranteedSupportMonths = source.GuaranteedSupportMonths, ResearchModifiers = new TypeResearchModifierPoints(),
            TechnologySnapshot = source.TechnologySnapshot, TechnologyModelVersion = source.TechnologyModelVersion,
        };
        return saved.TryGetFrozenDate(out var date) ? BuildAircraftForDate(family, type, date)
            : _aircraftFactory.CreateAircraft(family, type);
    }

    // Only mandatory selling prices change; schedules, quantities, guarantees and exact installed versions remain frozen.
    private ServiceOfferSnapshot RepriceMaintenanceOffer(ServiceOfferSnapshot offer)
    {
        offer.Validate();
        var aircraftGroup = _airframePartsGroups.GetGroupId(offer.AirframePartsGroupId) ?? offer.AirframePartsGroupId;
        var anchor = _savedAircraftDesigns.FirstOrDefault(d => d.CatalogItem.DesignId == aircraftGroup)
            ?? throw new InvalidOperationException("Airframe reference design is unavailable for maintenance pricing.");
        var engineTerms = offer.EngineMaintenance!;
        var exactEngine = _savedEngines.FirstOrDefault(e => e.ResearchId == engineTerms.EngineDesignId)
            ?? throw new InvalidOperationException("Exact installed engine is unavailable for maintenance pricing.");
        var engineGroup = _enginePartsGroups.GetGroupId(engineTerms.PartsGroupId) ?? engineTerms.PartsGroupId;
        var engineAnchor = _savedEngines.FirstOrDefault(e => e.ResearchId == engineGroup) ?? exactEngine;
        var unitPrice = ContractUiAirframePartPrice(anchor);
        var result = offer with
        {
            SparePartUnitPrice = unitPrice, ServicePricePerEvent = unitPrice * offer.SparePartsPerService,
            EngineMaintenance = engineTerms with
            {
                PartUnitPrice = ContractUiEnginePrices(engineAnchor).Part,
                ReplacementUnitPrice = ContractUiEnginePrices(exactEngine).Replacement,
            },
        };
        result.Validate();
        return result;
    }

    private Dictionary<string, EngineManufacturingCatalogItem> BuildEngineManufacturingCatalog()
    {
        RefreshPartsGroups();
        var result = new Dictionary<string, EngineManufacturingCatalogItem>(StringComparer.Ordinal);
        foreach (var saved in _savedEngines)
        {
            var group = _enginePartsGroups.GetGroupId(saved.ResearchId);
            if (string.IsNullOrWhiteSpace(group)) continue;
            result[saved.ResearchId] = new EngineManufacturingCatalogItem(saved.ResearchId, group,
                BuildSavedEngineAtReferenceDate(saved)) { IsPartsGroupAnchor = group == saved.ResearchId };
        }
        return result;
    }

    private bool IsPartsGroupAnchorInUse(string id, bool engine) => engine
        ? _enginePartsGroups.GetGroupId(id) is not null || _engineManufacturing.GetEngineUnits(id) > 0
            || _engineManufacturing.GetPartUnits(id) > 0 || _engineManufacturing.Sites.Any(s => s.EngineDesignId == id)
            || _engineManufacturing.CreateSnapshot().WorkOrders.Any(w => w.EngineDesignId == id || w.PartsGroupId == id)
        : _airframePartsGroups.GetGroupId(id) is not null || (_productionFactory?.GetAirframeSparePartUnits(id) ?? 0) > 0;

    private ServiceOfferSnapshot CreateCivilContractServiceOffer(WrightTycoon.Core.Aircraft.Aircraft aircraft,
        ContractRequirements requirements, double reliability)
    {
        var saved = _savedAircraftDesigns.FirstOrDefault(d => ReferenceEquals(d.CatalogItem.Aircraft, aircraft)
            || d.CatalogItem.Aircraft.TypeDefinition.Id == aircraft.TypeDefinition.Id);
        if (saved is null)
            return LifecycleCostCalculator.CreateContractServiceOffer(aircraft, requirements, reliability);
        return BuildCombinedMaintenanceOffer(saved, reliability, aircraft) with
        {
            GuaranteedSupportMonths = requirements.LifecycleModelVersion > 0 ? requirements.MinimumSupportMonths : aircraft.TypeDefinition.GuaranteedSupportMonths,
        };
    }

    private void DrawEngineSimilarity(Engine preview, string? referenceId)
    {
        if (string.IsNullOrWhiteSpace(referenceId))
        { GUILayout.Label("Save this engine, or develop a version, to compare its family and compatible parts."); return; }
        RefreshPartsGroups();
        var root = EngineFamilyRoot(referenceId);
        var group = _enginePartsGroups.GetGroupId(referenceId);
        GUILayout.Label($"Engine parts group: {(group is null ? "assigned on approval" : _savedEngines.FirstOrDefault(e => e.ResearchId == group)?.Name ?? group)}");
        _showEngineSimilarity = GUILayout.Toggle(_showEngineSimilarity, "Compare engine family (parts sharing: difference <=10%)");
        if (!_showEngineSimilarity) return;
        foreach (var other in _savedEngines.Where(e => EngineFamilyRoot(e.ResearchId) == root))
        {
            var comparison = MachineSimilarityCalculator.EngineDifference(preview, BuildSavedEngineAtReferenceDate(other));
            GUILayout.Label($"{other.Name}: {FormatUiNumber(comparison.DifferencePercent, "0.0")}% different · {(comparison.CanShareParts ? "parts-compatible" : "separate parts")}");
        }
        GUILayout.Label("Shared parts never imply automatic substitution of a complete engine. Groups check every member to prevent cumulative version drift.");
    }

}
