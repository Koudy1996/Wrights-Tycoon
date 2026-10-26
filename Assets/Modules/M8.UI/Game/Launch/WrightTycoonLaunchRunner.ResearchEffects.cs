using System;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Factories;
using WrightTycoon.Core.Research;

namespace WrightTycoon.Game.Launch;

public sealed partial class WrightTycoonLaunchRunner
{
    private AircraftFamilyDefinition ResolveFamilyResearchModifiers(AircraftFamilyDefinition family)
    {
        var key = BuildResearchModifierId(ResearchSubjectType.AircraftFamily, family.Id);
        return _researchModifiersByResearchId.TryGetValue(key, out var points)
            ? family.WithResearchModifiers(points) : family;
    }

    private static AircraftTypeDefinition WithTypeResearchModifiers(AircraftTypeDefinition source, TypeResearchModifierPoints points) => new()
    {
        Id = source.Id, FamilyId = source.FamilyId, DisplayName = source.DisplayName,
        TechnologySnapshot = source.TechnologySnapshot, TechnologyModelVersion = source.TechnologyModelVersion,
        EngineDelta = source.EngineDelta, AirframeDelta = source.AirframeDelta,
        NavigationEquipment = source.NavigationEquipment, HasPassengerCabin = source.HasPassengerCabin,
        HasCargoSpace = source.HasCargoSpace, WeaponPayloadKilograms = source.WeaponPayloadKilograms,
        HasSpecialEquipment = source.HasSpecialEquipment, HasCombatEquipment = source.HasCombatEquipment,
        ConstructionDurabilityLevel = source.ConstructionDurabilityLevel, ServiceIntervalHours = source.ServiceIntervalHours,
        GuaranteedSupportMonths = source.GuaranteedSupportMonths, ResearchModifiers = points.Clone(),
    };

    // Always build from raw parameters, not a previously discounted performance report.
    private Aircraft BuildResearchAircraft(SavedAircraftDesign saved, TypeResearchModifierPoints? familyOverride = null,
        TypeResearchModifierPoints? typeOverride = null)
    {
        if (_aircraftFactory is null) throw new InvalidOperationException("Aircraft factory is not initialized.");
        var family = ResolveFamilyDefinitionWithSavedEngineTechnology(saved);
        if (familyOverride is not null) family = family.WithResearchModifiers(familyOverride);
        var type = ResolveSavedTypeTechnology(saved);
        if (typeOverride is not null) type = WithTypeResearchModifiers(type, typeOverride);
        return saved.TryGetFrozenDate(out var date) ? BuildAircraftForDate(family, type, date)
            : _aircraftFactory.CreateAircraft(family, type);
    }

    private void RebuildResearchDependentCatalogs(ResearchSubjectType subject, string id)
    {
        if (_aircraftFactory is null) return;
        for (var index = 0; index < _savedAircraftDesigns.Count; index++)
        {
            var saved = _savedAircraftDesigns[index];
            if (!(subject == ResearchSubjectType.Engine && saved.EngineResearchId == id
                || subject == ResearchSubjectType.AircraftFamily && saved.FamilyResearchId == id
                || subject == ResearchSubjectType.AircraftType && saved.TypeResearchId == id)) continue;
            var points = GetResearchModifierPoints(ResearchSubjectType.AircraftType, saved.TypeResearchId);
            var aircraft = BuildResearchAircraft(saved, typeOverride: points);
            var catalog = new FactoryAircraftTypeCatalogItem(saved.CatalogItem.DesignId, saved.CatalogItem.DisplayName, aircraft);
            _savedAircraftDesigns[index] = CopySavedAircraftDesign(saved, catalog, saved.EngineResearchId);
        }
        _forecastCacheKey = string.Empty;
        _factoryForecastCacheKey = string.Empty;
        if (_researchBoard is not null)
            foreach (var project in _researchBoard.GetProjectsByStatus(ResearchStatus.Proposed))
                TryRefreshResearchProposalQuote(project);
    }
}
