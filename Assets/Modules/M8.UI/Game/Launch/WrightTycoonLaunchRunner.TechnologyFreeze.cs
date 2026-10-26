using System;
using System.Linq;
using System.Text;
using UnityEngine;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Research;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Game.Launch;

public sealed partial class WrightTycoonLaunchRunner
{
    private AirframeTechnologySnapshot CreateTechnologySnapshot(SimulationDate date, string origin = "approval") =>
        AirframeTechnologySnapshot.FromModifiers(date.Year, date.Month,
            _historicalCampaignTechnologyProgression.ResolveForDate(date), origin);

    private AirframeTechnologySnapshot? ResolveFamilyTechnology(string familyId)
    {
        var saved = _savedFamilies.FirstOrDefault(item => item.FamilyId == familyId);
        if (saved?.TechnologySnapshot is not null) return saved.TechnologySnapshot;
        if (saved is null || (_researchBoard is not null && _researchBoard.GetAllProjects().Any(project =>
            project.SubjectType == ResearchSubjectType.AircraftFamily && project.SubjectId == familyId && project.Status == ResearchStatus.Proposed)))
            return CreateTechnologySnapshot(_currentDate.NextMonth(), "preview");
        return null;
    }

    private void FreezeFamilyTechnology(string familyId, SimulationDate date)
    {
        var saved = _savedFamilies.FirstOrDefault(item => item.FamilyId == familyId);
        if (saved is not null && saved.TechnologySnapshot is null)
            saved.SetTechnologySnapshot(CreateTechnologySnapshot(date));
    }

    private Aircraft BuildStandaloneFamilyResearchAircraft(SavedFamilyDesign saved)
    {
        if (_aircraftFactory is null || saved.ReferenceFamilyDefinition is null || saved.ReferenceTypeDefinition is null)
            throw new InvalidOperationException("Standalone family research requires its saved reference configuration.");
        var date = _currentDate.NextMonth();
        var family = saved.ReferenceFamilyDefinition
            .WithTechnologySnapshot(saved.TechnologySnapshot ?? CreateTechnologySnapshot(date, "preview"))
            .WithResearchModifiers(new TypeResearchModifierPoints());
        var type = WithTypeTechnology(saved.ReferenceTypeDefinition, CreateTechnologySnapshot(date, "preview"), 2);
        // The embedded installed-engine recipe and technology are intentional: never substitute another saved engine.
        return BuildAircraftForDate(family, type, date);
    }

    private void BackfillFamilyTechnologySnapshots()
    {
        foreach (var family in _savedFamilies)
            if (family.TechnologySnapshot is null &&
                TryResolveApprovedDateFromResearchBoard(ResearchSubjectType.AircraftFamily, family.FamilyId, out var date))
                family.SetTechnologySnapshot(CreateTechnologySnapshot(date, "research-board-migration"));
    }

    private static AircraftTypeDefinition WithTypeTechnology(AircraftTypeDefinition source,
        AirframeTechnologySnapshot? snapshot, int version) => new()
    {
        Id = source.Id, FamilyId = source.FamilyId, DisplayName = source.DisplayName,
        EngineDelta = source.EngineDelta, AirframeDelta = source.AirframeDelta,
        NavigationEquipment = source.NavigationEquipment, HasPassengerCabin = source.HasPassengerCabin,
        HasCargoSpace = source.HasCargoSpace, WeaponPayloadKilograms = source.WeaponPayloadKilograms,
        HasSpecialEquipment = source.HasSpecialEquipment, HasCombatEquipment = source.HasCombatEquipment,
        ConstructionDurabilityLevel = source.ConstructionDurabilityLevel, ServiceIntervalHours = source.ServiceIntervalHours,
        GuaranteedSupportMonths = source.GuaranteedSupportMonths, ResearchModifiers = source.ResearchModifiers.Clone(),
        TechnologySnapshot = snapshot, TechnologyModelVersion = version,
    };

    private AircraftTypeDefinition ResolveSavedTypeTechnology(SavedAircraftDesign saved)
    {
        var type = saved.CatalogItem.Aircraft.TypeDefinition;
        if (saved.IsTypeVersionDevelopment)
        {
            var source = _savedAircraftDesigns.FirstOrDefault(item => item.TypeResearchId == saved.SourceTypeResearchId);
            if (source is not null) return WithTypeTechnology(type,
                source.CatalogItem.Aircraft.TypeDefinition.TechnologySnapshot,
                source.CatalogItem.Aircraft.TypeDefinition.TechnologyModelVersion);
        }
        if (saved.HasFrozenDate || saved.ApprovedYear.HasValue) return type;
        return ResolveFamilyTechnology(saved.FamilyResearchId) is null ? WithTypeTechnology(type, null, 1)
            : WithTypeTechnology(type, CreateTechnologySnapshot(_currentDate.NextMonth(), "preview"), 2);
    }

    private void FreezeTypeTechnology(int index, SimulationDate date)
    {
        var saved = _savedAircraftDesigns[index];
        var family = ResolveFamilyDefinitionWithSavedEngineTechnology(saved);
        if (family.TechnologySnapshot is null) return;
        var type = WithTypeTechnology(saved.CatalogItem.Aircraft.TypeDefinition, CreateTechnologySnapshot(date), 2);
        var aircraft = BuildAircraftForDate(family, type, date);
        _savedAircraftDesigns[index] = CopySavedAircraftDesign(saved,
            new WrightTycoon.Core.Factories.FactoryAircraftTypeCatalogItem(saved.CatalogItem.DesignId,
                saved.CatalogItem.DisplayName, aircraft), saved.EngineResearchId);
    }

    private AircraftTypeDefinition ApplyDraftTypeTechnology(AircraftTypeDefinition type, string familyId)
    {
        SavedAircraftDesign? source = null;
        if (TryGetEditingSavedType(out var editing, out _)) source = editing;
        if (_typeVersionDevelopment is not null)
            source = _savedAircraftDesigns.FirstOrDefault(item => item.TypeResearchId == _typeVersionDevelopment.SourceTypeResearchId);
        if (source is not null && (source.HasFrozenDate || source.ApprovedYear.HasValue || source.IsTypeVersionDevelopment))
        {
            var inherited = ResolveSavedTypeTechnology(source);
            return WithTypeTechnology(type, inherited.TechnologySnapshot, inherited.TechnologyModelVersion);
        }
        return ResolveFamilyTechnology(familyId) is null ? type
            : WithTypeTechnology(type, CreateTechnologySnapshot(_currentDate.NextMonth(), "preview"), 2);
    }

    private static void ValidateTechnologySaveData(RunnerGameSavePayload data)
    {
        if (data.SavedFamilies is not null)
            foreach (var family in data.SavedFamilies)
            {
                family?.TechnologySnapshot?.Validate();
                family?.ReferenceFamilyDefinition?.TechnologySnapshot?.Validate();
                family?.ReferenceTypeDefinition?.TechnologySnapshot?.Validate();
            }
        if (data.SavedAircraftDesigns is null) return;
        foreach (var saved in data.SavedAircraftDesigns)
        {
            if (saved?.TypeDefinition is null) continue;
            var type = saved.TypeDefinition;
            if (type.TechnologyModelVersion != 1 && type.TechnologyModelVersion != 2)
                throw new InvalidOperationException("Unsupported aircraft technology model.");
            type.TechnologySnapshot?.Validate();
            saved.FamilyDefinition?.TechnologySnapshot?.Validate();
            if (type.TechnologyModelVersion == 2 &&
                (type.TechnologySnapshot is null || saved.FamilyDefinition?.TechnologySnapshot is null))
                throw new InvalidOperationException("Weighted aircraft technology requires both frozen snapshots.");
            if (type.TechnologySnapshot is not null && saved.FrozenYear.HasValue &&
                (type.TechnologySnapshot.Year != saved.FrozenYear || type.TechnologySnapshot.Month != saved.FrozenMonth))
                throw new InvalidOperationException("Type technology date disagrees with its frozen approval date.");
        }
    }

    private void DrawTechnologyFreezeSummary()
    {
        if (_aircraftPreview is null) return;
        var family = _aircraftPreview.FamilyDefinition.TechnologySnapshot;
        var type = _aircraftPreview.TypeDefinition;
        string DateLabel(AirframeTechnologySnapshot? snapshot) => snapshot is null ? "Not frozen"
            : $"{snapshot.Month:00}/{snapshot.Year}" + (snapshot.Origin == "preview" ? " (approval preview)" : "");
        var text = $"Family technology: {DateLabel(family)} | Type technology: {DateLabel(type.TechnologySnapshot)}";
        if (type.TechnologyModelVersion != 2 || family is null || type.TechnologySnapshot is null)
        {
            DrawModifierLabelWithTooltip(text + " | Legacy technology", new ModifierTooltipDefinition(
                "technology.freeze.legacy", "Legacy technology",
                neutralLines: new[] { "Original technology model; no family/type weighting.",
                    "A missing historical family approval date is not invented." }));
            return;
        }
        var effects = AirframeTechnologyEffects.Resolve(family, type.TechnologySnapshot);
        var tooltip = new StringBuilder("Technology indices (not percentage savings):\n");
        void Row(string label, double weight, double f, double t, double result, bool multiply = false) =>
            tooltip.AppendLine($"{label}: family {weight:P0} / type {1d-weight:P0}; F {f:0.000}; T {t:0.000}; index {result:0.000}; effect {(multiply ? result-1d : 1d/result-1d):+0.0%;-0.0%;0.0%}");
        var fs = family.AirframeEfficiencyMultiplier; var ts = type.TechnologySnapshot.AirframeEfficiencyMultiplier;
        var fa = family.AerodynamicsEfficiencyMultiplier; var ta = type.TechnologySnapshot.AerodynamicsEfficiencyMultiplier;
        Row("Fuselage mass", AirframeTechnologyBalance.FuselageMassFamilyShare, fs, ts, effects.FuselageMass);
        Row("Wing mass", AirframeTechnologyBalance.WingMassFamilyShare, fs, ts, effects.WingMass);
        Row("Systems mass", AirframeTechnologyBalance.SystemsMassFamilyShare, fs, ts, effects.SystemsMass);
        Row("Body drag", AirframeTechnologyBalance.BodyDragFamilyShare, fa, ta, effects.BodyDrag);
        Row("Profile drag", AirframeTechnologyBalance.ProfileDragFamilyShare, fa, ta, effects.ProfileDrag);
        Row("Lift", AirframeTechnologyBalance.LiftFamilyShare, fa, ta, effects.Lift, true);
        Row("Materials", AirframeTechnologyBalance.MaterialsFamilyShare, fs, ts, effects.Materials);
        Row("Work", AirframeTechnologyBalance.WorkFamilyShare, fs, ts, effects.Work);
        DrawModifierLabelWithTooltip(text, new ModifierTooltipDefinition("technology.freeze", "Frozen family / type technology",
            neutralLines: tooltip.ToString().Split('\n')));
    }
}
