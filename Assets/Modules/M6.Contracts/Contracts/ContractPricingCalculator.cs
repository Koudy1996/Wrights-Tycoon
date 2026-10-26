using System;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Campaign;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.Contracts;

public sealed class ContractPricingCalculator
{
    private readonly IHistoricalCampaignContractModifierSource? _campaignModifierSource;
    private readonly Func<double>? _reputationPriceMultiplierProvider;

    public ContractPricingCalculator(IHistoricalCampaignContractModifierSource? campaignModifierSource = null, Func<double>? reputationPriceMultiplierProvider = null)
    {
        _campaignModifierSource = campaignModifierSource;
        _reputationPriceMultiplierProvider = reputationPriceMultiplierProvider;
    }

    public ContractPricingQuote CalculateQuote(ContractRequirements requirements, int quantityUnits)
    {
        if (requirements is null)
        {
            throw new ArgumentNullException(nameof(requirements));
        }

        if (quantityUnits < ContractBalance.MinQuantityUnits || quantityUnits > ContractBalance.MaxQuantityUnits)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantityUnits),
                $"Quantity must be within [{ContractBalance.MinQuantityUnits}, {ContractBalance.MaxQuantityUnits}].");
        }

        requirements.Validate();

        if (requirements.EconomicSnapshot is { } economic)
            return new ContractPricingQuote(economic.ReferenceDirectUnitCost, economic.OfferedUnitPrice,
                RoundCurrency(economic.OfferedUnitPrice * quantityUnits), economic.OfferedUnitPrice / economic.ReferenceDirectUnitCost - 1d);

        var estimatedMinimumUnitCost = EstimateMinimumUnitProductionCost(requirements);
        var markup = quantityUnits < ContractBalance.LargeOrderMinQuantityUnits
            ? ContractBalance.SmallOrderContractMarkup
            : ContractBalance.LargeOrderContractMarkup;
        var priceMultiplier = ResolvePriceMultiplier(requirements.RequiredCategory);
        var adjustedMarkup = ((1d + markup)
            * priceMultiplier
            * ContractBalance.ContractPriceMarginImprovementMultiplier) - 1d;
        if (adjustedMarkup < ContractBalance.CampaignAdjustedMarkupFloor)
        {
            adjustedMarkup = ContractBalance.CampaignAdjustedMarkupFloor;
        }

        adjustedMarkup *= ContractBalance.AdditionalMarginMultiplier;
        var unitPrice = estimatedMinimumUnitCost * (1d + adjustedMarkup) * LifecycleCostCalculator.ReputationMultiplier(_reputationPriceMultiplierProvider);
        if (requirements.LifecycleModelVersion > 0 && requirements.ProcurementPreference == ProcurementPreference.WholeLifeCost)
        {
            var fuelRange = ContractBalance.ResolveFuelConsumptionRequirementRangeKilogramsPerHour(requirements.RequiredCategory ?? AircraftCategory.LightSportAndRecreational);
            var category = requirements.RequiredCategory ?? AircraftCategory.LightSportAndRecreational;
            var operation = LifecycleContractPolicy.ReferenceOperationQuote(category, estimatedMinimumUnitCost, requirements.MinimumExpectedLifeHours,
                    requirements.MaximumServiceIntervalHours, requirements.MinimumSupportMonths, purchasePrice: 0d,
                    fuelBurnKilogramsPerHour: requirements.MaximumFuelConsumptionKilogramsPerHour ?? fuelRange.Item2);
            unitPrice = System.Math.Min(unitPrice, System.Math.Max(0d, requirements.MaximumOperatingCostPerUnit
                - operation.OperationCosts));
        }
        var totalPrice = unitPrice * quantityUnits;

        return new ContractPricingQuote(
            RoundCurrency(estimatedMinimumUnitCost),
            RoundCurrency(unitPrice),
            RoundCurrency(totalPrice),
            adjustedMarkup);
    }

    public double EstimateMinimumUnitProductionCost(ContractRequirements requirements)
    {
        if (requirements is null)
        {
            throw new ArgumentNullException(nameof(requirements));
        }

        requirements.Validate();

        if (requirements.RequiredCategory.HasValue)
        {
            var profile = AircraftCategoryCatalog.GetProfile(requirements.RequiredCategory.Value);
            return EstimateByProfile(requirements, profile);
        }

        var profiles = AircraftCategoryCatalog.GetAllProfiles();
        if (profiles.Count == 0)
        {
            return ContractBalance.PricingMinimumUnitCostFloor;
        }

        var total = 0d;
        for (var index = 0; index < profiles.Count; index++)
        {
            total += EstimateByProfile(requirements, profiles[index]);
        }

        return total / profiles.Count;
    }

    private static double EstimateByProfile(ContractRequirements requirements, AircraftCategoryProfile profile)
    {
        var difficulty = CalculateDifficultyNormalized(requirements, profile);

        var structuralIndexRange = ContractBalance.ResolveReferenceStructuralIndexRange(profile.Category);
        var structuralIndex = structuralIndexRange.Min
            + ((structuralIndexRange.Max - structuralIndexRange.Min) * difficulty);
        var estimated = CalculateReferenceCompleteProductionCost(profile, structuralIndex, difficulty);
        return System.Math.Max(ContractBalance.PricingMinimumUnitCostFloor, estimated);
    }

    private static double CalculateReferenceCompleteProductionCost(AircraftCategoryProfile profile, double structuralIndex, double difficulty) =>
        CalculateReferenceEstimate(profile, structuralIndex, difficulty).DirectUnitCost;

    internal static ContractReferenceEstimate CalculateReferenceEstimate(
        AircraftCategoryProfile profile,
        double structuralIndex,
        double difficulty)
    {
        var category = profile.Category;
        var airframeParts = ProductionEconomyBalance.CalculateAirframeBaseParts(structuralIndex);
        var airframeManDays = ProductionEconomyBalance.CalculateAirframeBaseManDays(structuralIndex);
        var policy = AircraftCategoryRequirementBalance.GetPolicy(category);

        if (category == AircraftCategory.LightSportAndRecreational)
        {
            airframeParts *= ProductionEconomyBalance.Airframe.WoodPartsMultiplier
                * ProductionEconomyBalance.Airframe.TrussPartsMultiplier
                * AircraftConstructionStandardBalance.GetEffects(AircraftConstructionStandard.Ultralight).ProductionCostMultiplier;
            airframeManDays *= ProductionEconomyBalance.Airframe.WoodManDaysMultiplier
                * ProductionEconomyBalance.Airframe.TrussManDaysMultiplier
                * AircraftConstructionStandardBalance.GetEffects(AircraftConstructionStandard.Ultralight).ProductionManDaysMultiplier;
        }
        else
        {
            var standard = (policy.AllowedConstructionStandards & AircraftConstructionStandardOptions.ArmyGrade) != 0
                && (policy.AllowedConstructionStandards & AircraftConstructionStandardOptions.Civil) == 0
                    ? AircraftConstructionStandard.ArmyGrade
                    : AircraftConstructionStandard.Civil;
            var standardEffects = AircraftConstructionStandardBalance.GetEffects(standard);
            airframeParts *= standardEffects.ProductionCostMultiplier;
            airframeManDays *= standardEffects.ProductionManDaysMultiplier;
        }

        airframeParts *= 1d + ProductionEconomyBalance.GetRolePartsRate(policy.RoleRequirement);
        airframeManDays *= 1d + ProductionEconomyBalance.GetRoleManDaysRate(policy.RoleRequirement);

        var requiredRange = profile.MinRequiredRangeKilometers
            + ((profile.MaxRequiredRangeKilometers - profile.MinRequiredRangeKilometers) * difficulty);
        if (requiredRange > 4000d)
        {
            airframeParts += System.Math.Max(
                ProductionEconomyBalance.Airframe.AdvancedNavigationMinimumParts,
                airframeParts * ProductionEconomyBalance.Airframe.AdvancedNavigationPartsRate);
            airframeManDays += System.Math.Max(
                ProductionEconomyBalance.Airframe.AdvancedNavigationMinimumManDays,
                airframeManDays * ProductionEconomyBalance.Airframe.AdvancedNavigationManDaysRate);
        }
        else if (requiredRange > 1500d)
        {
            airframeParts += System.Math.Max(
                ProductionEconomyBalance.Airframe.BasicNavigationMinimumParts,
                airframeParts * ProductionEconomyBalance.Airframe.BasicNavigationPartsRate);
            airframeManDays += System.Math.Max(
                ProductionEconomyBalance.Airframe.BasicNavigationMinimumManDays,
                airframeManDays * ProductionEconomyBalance.Airframe.BasicNavigationManDaysRate);
        }

        var engineCount = EstimateEngineCount(category, profile.MinMaximumWeightKilograms
            + ((profile.MaxMaximumWeightKilograms - profile.MinMaximumWeightKilograms) * difficulty));
        var engine = CalculateReferenceEngineCost(category, structuralIndex);
        return new ContractReferenceEstimate(airframeParts, airframeManDays, engineCount, engine.Parts, engine.ManDays,
            !IsJetCategory(category), 0d, .8d);
    }

    private static (double Parts, double ManDays) CalculateReferenceEngineCost(AircraftCategory category, double structuralIndex)
    {
        if (IsJetCategory(category))
        {
            var diameter = Numeric.Clamp(1.2d * System.Math.Pow(structuralIndex / 180d, 0.25d), 0.8d, 3d);
            var isTurbojet = category is AircraftCategory.MilitaryTactical or AircraftCategory.Bomber;
            var baseParts = isTurbojet
                ? ProductionEconomyBalance.Engine.TurbojetReferenceParts
                : ProductionEconomyBalance.Engine.TurbofanReferenceParts;
            var baseManDays = isTurbojet
                ? ProductionEconomyBalance.Engine.TurbojetReferenceManDays
                : ProductionEconomyBalance.Engine.TurbofanReferenceManDays;
            var sizeMultiplier = System.Math.Pow(
                diameter / ProductionEconomyBalance.Engine.JetReferenceDiameterMeters,
                ProductionEconomyBalance.Engine.JetSizeExponent);
            var compression = isTurbojet ? 20d : 30d;
            var jetParts = baseParts * sizeMultiplier * ProductionEconomyBalance.GetJetCompressionPartsMultiplier(compression);
            var jetManDays = baseManDays * sizeMultiplier * ProductionEconomyBalance.GetJetCompressionManDaysMultiplier(compression);
            if (!isTurbojet)
            {
                jetParts *= 1d + (6d * ProductionEconomyBalance.Engine.BypassPartsIncreasePerPoint);
                jetManDays *= 1d + (6d * ProductionEconomyBalance.Engine.BypassManDaysIncreasePerPoint);
            }

            return (jetParts, jetManDays);
        }

        var displacement = Numeric.Clamp(
            2.4d * System.Math.Pow(structuralIndex / ProductionEconomyBalance.Airframe.ReferenceStructuralIndex, 0.65d),
            2.4d,
            100d);
        var cylinders = (int)Numeric.Clamp(
            System.Math.Round(4d + (10d * Numeric.Clamp(structuralIndex / 375d, 0d, 1d))),
            4d,
            18d);
        var cylinderVolume = displacement / cylinders;
        var parts = ProductionEconomyBalance.Engine.PistonBaseParts
            + (cylinders * ProductionEconomyBalance.Engine.PistonCylinderParts * System.Math.Pow(
                cylinderVolume / ProductionEconomyBalance.Engine.PistonReferenceCylinderVolumeLiters,
                ProductionEconomyBalance.Engine.PistonCylinderPartsExponent))
            + (displacement * ProductionEconomyBalance.Engine.PistonDisplacementPartsPerLiter);
        var manDays = ProductionEconomyBalance.Engine.PistonBaseManDays
            + (cylinders * ProductionEconomyBalance.Engine.PistonCylinderManDays * System.Math.Pow(
                cylinderVolume / ProductionEconomyBalance.Engine.PistonReferenceCylinderVolumeLiters,
                ProductionEconomyBalance.Engine.PistonCylinderManDaysExponent))
            + (displacement * ProductionEconomyBalance.Engine.PistonDisplacementManDaysPerLiter);
        parts *= ProductionEconomyBalance.GetPistonCompressionPartsMultiplier(8d);
        manDays *= ProductionEconomyBalance.GetPistonCompressionManDaysMultiplier(8d);
        return (parts, manDays);
    }

    private static bool IsJetCategory(AircraftCategory category) => category is
        AircraftCategory.RegionalJet
        or AircraftCategory.MediumTransportNarrowBody
        or AircraftCategory.HeavyTransportWideBody
        or AircraftCategory.BusinessVipJet
        or AircraftCategory.MilitaryTactical
        or AircraftCategory.MilitaryStrategicSupport
        or AircraftCategory.LongRangeCargo
        or AircraftCategory.Bomber;

    private static double CalculateReferenceUnitHardwareCost(
        double maxWeightKilograms,
        double rangeKilometers,
        double speedKilometersPerHour,
        double payloadKilograms,
        int crew)
    {
        return ContractBalance.PricingBaseUnitCost
            + (maxWeightKilograms * ContractBalance.PricingWeightCoefficient)
            + (rangeKilometers * ContractBalance.PricingRangeCoefficient)
            + (speedKilometersPerHour * ContractBalance.PricingSpeedCoefficient)
            + (payloadKilograms * ContractBalance.PricingPayloadCoefficient)
            + (crew * ContractBalance.PricingCrewCoefficient);
    }

    private static double CalculateReferenceUnitManDayLaborCost(
        AircraftCategory category,
        double maxWeightKilograms,
        double rangeKilometers,
        double speedKilometersPerHour,
        double payloadKilograms,
        int crew)
    {
        var airframeManDays = CalculateEstimatedAirframeProductionManDays(
            maxWeightKilograms,
            rangeKilometers,
            speedKilometersPerHour,
            payloadKilograms,
            crew);

        var engineCount = EstimateEngineCount(category, maxWeightKilograms);
        var engineManDaysPerEngine = CalculateEstimatedEngineProductionManDaysPerEngine(
            maxWeightKilograms,
            speedKilometersPerHour,
            payloadKilograms,
            crew);
        var totalEngineManDays = engineManDaysPerEngine * engineCount;

        var planeLaborCostPerManDay = FactoryBalance.PlaneStaffMonthlyCostPerEmployee
            / System.Math.Max(FactoryBalance.EmployeeManDaysPerMonth, Numeric.Epsilon);
        var engineLaborCostPerManDay = FactoryBalance.EngineStaffMonthlyCostPerEmployee
            / System.Math.Max(FactoryBalance.EmployeeManDaysPerMonth, Numeric.Epsilon);

        return (airframeManDays * planeLaborCostPerManDay)
            + (totalEngineManDays * engineLaborCostPerManDay);
    }

    private static double CalculateEstimatedAirframeProductionManDays(
        double maxWeightKilograms,
        double rangeKilometers,
        double speedKilometersPerHour,
        double payloadKilograms,
        int crew)
    {
        var estimated = ContractBalance.PricingAirframeProductionManDaysBase
            + (maxWeightKilograms * ContractBalance.PricingAirframeProductionManDaysWeightCoefficient)
            + (rangeKilometers * ContractBalance.PricingAirframeProductionManDaysRangeCoefficient)
            + (speedKilometersPerHour * ContractBalance.PricingAirframeProductionManDaysSpeedCoefficient)
            + (payloadKilograms * ContractBalance.PricingAirframeProductionManDaysPayloadCoefficient)
            + (crew * ContractBalance.PricingAirframeProductionManDaysCrewCoefficient);

        return System.Math.Max(estimated, ContractBalance.PricingAirframeProductionManDaysMinimum);
    }

    private static double CalculateEstimatedEngineProductionManDaysPerEngine(
        double maxWeightKilograms,
        double speedKilometersPerHour,
        double payloadKilograms,
        int crew)
    {
        var estimated = ContractBalance.PricingEngineProductionManDaysPerEngineBase
            + (maxWeightKilograms * ContractBalance.PricingEngineProductionManDaysPerEngineWeightCoefficient)
            + (speedKilometersPerHour * ContractBalance.PricingEngineProductionManDaysPerEngineSpeedCoefficient)
            + (payloadKilograms * ContractBalance.PricingEngineProductionManDaysPerEnginePayloadCoefficient)
            + (crew * ContractBalance.PricingEngineProductionManDaysPerEngineCrewCoefficient);

        return System.Math.Max(estimated, ContractBalance.PricingEngineProductionManDaysPerEngineMinimum);
    }

    private static int EstimateEngineCount(AircraftCategory category, double maxWeightKilograms)
    {
        var estimated = maxWeightKilograms switch
        {
            <= ContractBalance.PricingEstimatedEngineCountSingleMaxWeightKilograms => 1,
            <= ContractBalance.PricingEstimatedEngineCountDoubleMaxWeightKilograms => 2,
            <= ContractBalance.PricingEstimatedEngineCountTripleMaxWeightKilograms => 3,
            _ => 4,
        };

        if (category is AircraftCategory.HeavyTransportWideBody or AircraftCategory.MilitaryStrategicSupport)
        {
            estimated = System.Math.Max(estimated, 2);
        }

        if (category == AircraftCategory.LightSportAndRecreational)
        {
            estimated = 1;
        }

        return (int)Numeric.Clamp(
            estimated,
            ContractBalance.PricingEstimatedEngineCountMin,
            ContractBalance.PricingEstimatedEngineCountMax);
    }

    internal static double CalculateDifficultyNormalized(ContractRequirements requirements, AircraftCategoryProfile profile)
    {
        var sum = 0d;
        var count = 0;

        if (requirements.MinimumReliability.HasValue)
        {
            AddMetric(ref sum, ref count, Normalize(
                requirements.MinimumReliability.Value,
                ContractBalance.MinReliability,
                ContractBalance.MaxReliability,
                invert: false));
        }

        if (requirements.MaximumTotalWeightKilograms.HasValue)
        {
            AddMetric(ref sum, ref count, Normalize(
                requirements.MaximumTotalWeightKilograms.Value,
                profile.MinMaximumWeightKilograms,
                profile.MaxMaximumWeightKilograms,
                invert: false));
        }

        if (requirements.MinimumRangeKilometers.HasValue)
        {
            AddMetric(ref sum, ref count, Normalize(
                requirements.MinimumRangeKilometers.Value,
                profile.MinRequiredRangeKilometers,
                profile.MaxRequiredRangeKilometers,
                invert: false));
        }

        if (requirements.MinimumSpeed.HasValue)
        {
            var minimumSpeedKmh = requirements.MinimumSpeed.Value.MinimumSpeedMetersPerSecond * 3.6d;
            AddMetric(ref sum, ref count, Normalize(
                minimumSpeedKmh,
                profile.MinRequiredSpeedKilometersPerHour,
                profile.MaxRequiredSpeedKilometersPerHour,
                invert: false));
        }

        if (requirements.MinimumPayloadKilograms.HasValue)
        {
            AddMetric(ref sum, ref count, Normalize(
                requirements.MinimumPayloadKilograms.Value,
                profile.MinRequiredPayloadKilograms,
                profile.MaxRequiredPayloadKilograms,
                invert: false));
        }

        if (requirements.MinimumManeuverability.HasValue)
        {
            AddMetric(ref sum, ref count, Normalize(
                requirements.MinimumManeuverability.Value,
                profile.MinRequiredManeuverability,
                profile.MaxRequiredManeuverability,
                invert: false));
        }

        if (requirements.MinimumCrew.HasValue)
        {
            AddMetric(ref sum, ref count, Normalize(
                requirements.MinimumCrew.Value,
                profile.MinCrew,
                profile.MaxCrew,
                invert: false));
        }

        if (requirements.MaximumFuelConsumptionKilogramsPerHour.HasValue)
        {
            var (minimumFuelConsumption, maximumFuelConsumption) =
                ContractBalance.ResolveFuelConsumptionRequirementRangeKilogramsPerHour(profile.Category);
            AddMetric(ref sum, ref count, Normalize(
                requirements.MaximumFuelConsumptionKilogramsPerHour.Value,
                minimumFuelConsumption,
                maximumFuelConsumption,
                invert: true));
        }

        if (count == 0)
        {
            return 0.5d;
        }

        return Numeric.Clamp(sum / count, 0d, 1d);
    }

    private static double Normalize(double value, double minValue, double maxValue, bool invert)
    {
        if (maxValue - minValue <= Numeric.Epsilon)
        {
            return 0.5d;
        }

        var normalized = Numeric.Clamp((value - minValue) / (maxValue - minValue), 0d, 1d);
        return invert ? 1d - normalized : normalized;
    }

    private static void AddMetric(ref double sum, ref int count, double value)
    {
        sum += value;
        count++;
    }

    private double ResolvePriceMultiplier(AircraftCategory? category)
    {
        var modifiers = _campaignModifierSource?.GetCurrentModifiers();
        if (modifiers is null)
        {
            return 1d;
        }

        return modifiers.ResolvePriceMultiplier(category);
    }

    public ContractEconomicSnapshot CreateEconomicSnapshot(ContractRequirements requirements,
        WrightTycoon.Core.Simulation.SimulationDate date, int quantity, OperationalCostPriority? priority = null)
    {
        var reference = new ReferenceProductionCostEstimator().Estimate(requirements, date);
        var reputation = LifecycleCostCalculator.ReputationMultiplier(_reputationPriceMultiplierProvider);
        var historical = ResolvePriceMultiplier(requirements.RequiredCategory);
        var price = ContractPricingBalance.PurchasePrice(reference.DirectUnitCost, quantity, reputation, historical);
        var fuel = new WrightTycoon.Core.Fleet.MonthlyAviationFuelMarket().Get(date, reference.UsesAviationGasoline).DollarsPerKilogram;
        var operation = ContractOperatingBudgetCalculator.ReferenceCostPerHour(reference, price, requirements, fuel);
        var selectedPriority = priority ?? ContractPricingBalance.Priority(requirements.ProcurementPreference);
        var result = new ContractEconomicSnapshot(1, date, reference, ContractPricingBalance.OverheadMultiplier,
            ContractPricingBalance.MarkupMultiplier, ContractPricingBalance.QuantityMultiplier(quantity), reputation,
            historical, price, operation, operation * ContractPricingBalance.OperatingTolerance(selectedPriority), selectedPriority, fuel);
        result.Validate();
        return result;
    }

    private static double RoundCurrency(double value) =>
        System.Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
