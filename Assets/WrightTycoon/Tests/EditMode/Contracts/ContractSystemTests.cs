using System.Collections.Generic;
using NUnit.Framework;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Campaign;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Contracts;
using WrightTycoon.Core.Engines;
using WrightTycoon.Core.Factories;
using WrightTycoon.Core.Units;
using AircraftModel = WrightTycoon.Core.Aircraft.Aircraft;

namespace WrightTycoon.Core.Tests.Contracts;

[TestFixture]
public sealed class ContractSystemTests
{
    [Test]
    public void Generator_CreatesValidContractBatchWithinM6Bounds()
    {
        var generator = new ContractGenerator(seed: 1234);
        var contracts = generator.GenerateMonthlyContracts(monthIndex: 0);

        Assert.That(contracts.Count, Is.InRange(1, 5));
        for (var index = 0; index < contracts.Count; index++)
        {
            var contract = contracts[index];
            Assert.That(contract.QuantityUnits, Is.InRange(5, 500));
            Assert.That(contract.DeadlineMonths, Is.InRange(6, 36));
            Assert.That(contract.Requirements.DefinedRequirementCount, Is.InRange(6, 8));
            Assert.That(contract.Requirements.RequiredCategory.HasValue, Is.True);
            Assert.That(contract.Requirements.MaximumRequiredRunwayLengthMeters.HasValue, Is.True);
            Assert.That(contract.Requirements.MinimumRangeKilometers.HasValue, Is.True);
            Assert.That(contract.Requirements.MinimumSpeed.HasValue, Is.True);
            Assert.That(contract.Requirements.MinimumPayloadKilograms.HasValue, Is.True);
            Assert.That(contract.Requirements.MinimumManeuverability.HasValue, Is.True);
            Assert.That(contract.Requirements.MinimumCrew.HasValue, Is.True);
            var allowedRunwayLengths = ContractBalance.ResolveAllowedRunwayLengthMeters(contract.Requirements.RequiredCategory!.Value);
            Assert.That(allowedRunwayLengths, Has.Member(contract.Requirements.MaximumRequiredRunwayLengthMeters.Value));
            Assert.That(contract.EstimatedMinimumUnitCost, Is.GreaterThan(0d));
            Assert.That(contract.UnitPrice, Is.GreaterThan(contract.EstimatedMinimumUnitCost));
            Assert.That(contract.TotalPrice, Is.GreaterThan(0d));

            var profile = AircraftCategoryCatalog.GetProfileForYear(
                contract.Requirements.RequiredCategory!.Value,
                ResolveGenerationYear(monthIndex: 0));
            if (contract.Requirements.MaximumTotalWeightKilograms.HasValue)
            {
                Assert.That(contract.Requirements.MaximumTotalWeightKilograms.Value, Is.InRange(profile.MinMaximumWeightKilograms, profile.MaxMaximumWeightKilograms));
            }

            if (contract.Requirements.MinimumRangeKilometers.HasValue)
            {
                Assert.That(contract.Requirements.MinimumRangeKilometers.Value, Is.InRange(profile.MinRequiredRangeKilometers, profile.MaxRequiredRangeKilometers));
            }

            if (contract.Requirements.MinimumPayloadKilograms.HasValue)
            {
                Assert.That(contract.Requirements.MinimumPayloadKilograms.Value, Is.InRange(profile.MinRequiredPayloadKilograms, profile.MaxRequiredPayloadKilograms));
            }

            if (contract.Requirements.MinimumManeuverability.HasValue)
            {
                Assert.That(contract.Requirements.MinimumManeuverability.Value, Is.InRange(profile.MinRequiredManeuverability, profile.MaxRequiredManeuverability));
            }

            if (contract.Requirements.MinimumCrew.HasValue)
            {
                Assert.That(contract.Requirements.MinimumCrew.Value, Is.InRange(profile.MinCrew, profile.MaxCrew));
            }

            if (contract.Requirements.MinimumSpeed.HasValue)
            {
                var requiredSpeedKmh = contract.Requirements.MinimumSpeed.Value.MinimumSpeedMetersPerSecond * 3.6d;
                Assert.That(requiredSpeedKmh, Is.InRange(profile.MinRequiredSpeedKilometersPerHour, profile.MaxRequiredSpeedKilometersPerHour));
            }
        }
    }

    [Test]
    public void Generator_FillsMissingCoreRequirements_WithCurrentCategoryMinimums()
    {
        const int monthIndex = 36;
        var options = new ContractGeneratorOptions
        {
            MinContractsPerMonth = 1,
            MaxContractsPerMonth = 1,
            RequestedCategories = new[] { AircraftCategory.RegionalJet },
        };

        var generator = new ContractGenerator(seed: 1234, options: options);
        var contract = generator.GenerateMonthlyContracts(monthIndex)[0];
        var requirements = contract.Requirements;
        var profile = AircraftCategoryCatalog.GetProfileForYear(
            AircraftCategory.RegionalJet,
            ResolveGenerationYear(monthIndex));

        Assert.That(requirements.MinimumRangeKilometers.HasValue, Is.True);
        Assert.That(requirements.MinimumRangeKilometers!.Value, Is.GreaterThanOrEqualTo(profile.MinRequiredRangeKilometers));
        Assert.That(requirements.MinimumPayloadKilograms.HasValue, Is.True);
        Assert.That(requirements.MinimumPayloadKilograms!.Value, Is.GreaterThanOrEqualTo(profile.MinRequiredPayloadKilograms));
        Assert.That(requirements.MinimumManeuverability.HasValue, Is.True);
        Assert.That(requirements.MinimumManeuverability!.Value, Is.GreaterThanOrEqualTo(profile.MinRequiredManeuverability));
        Assert.That(requirements.MinimumCrew.HasValue, Is.True);
        Assert.That(requirements.MinimumCrew!.Value, Is.GreaterThanOrEqualTo(profile.MinCrew));
        Assert.That(requirements.MinimumSpeed.HasValue, Is.True);
        Assert.That(
            requirements.MinimumSpeed!.Value.MinimumSpeedMetersPerSecond,
            Is.GreaterThanOrEqualTo(profile.MinRequiredSpeedMetersPerSecond));
    }

    [Test]
    public void RunwayBalance_UsesSingleTierForExtremeCategories_AndTwoTiersForIntermediateCategories()
    {
        CollectionAssert.AreEqual(
            new[] { PlaneDesignerBalance.Airframe.RunwayShortLengthMeters },
            ContractBalance.ResolveAllowedRunwayLengthMeters(AircraftCategory.LightSportAndRecreational));
        CollectionAssert.AreEqual(
            new[] { PlaneDesignerBalance.Airframe.RunwayShortLengthMeters, PlaneDesignerBalance.Airframe.RunwayMediumLengthMeters },
            ContractBalance.ResolveAllowedRunwayLengthMeters(AircraftCategory.LightMultiRolePiston));
        CollectionAssert.AreEqual(
            new[] { PlaneDesignerBalance.Airframe.RunwayMediumLengthMeters, PlaneDesignerBalance.Airframe.RunwayLongLengthMeters },
            ContractBalance.ResolveAllowedRunwayLengthMeters(AircraftCategory.RegionalJet));
        CollectionAssert.AreEqual(
            new[] { PlaneDesignerBalance.Airframe.RunwayLongLengthMeters },
            ContractBalance.ResolveAllowedRunwayLengthMeters(AircraftCategory.HeavyTransportWideBody));
    }

    [Test]
    public void Generator_SpreadsRequestedCategoriesEvenlyAcrossOrderCycles()
    {
        var requestedCategories = new[]
        {
            AircraftCategory.LightSportAndRecreational,
            AircraftCategory.LightMultiRolePiston,
            AircraftCategory.TurbopropRegional,
            AircraftCategory.RegionalJet,
        };

        var options = new ContractGeneratorOptions
        {
            MinContractsPerMonth = requestedCategories.Length,
            MaxContractsPerMonth = requestedCategories.Length,
            RequestedCategories = requestedCategories,
        };

        var generator = new ContractGenerator(seed: 2206, options: options);
        var firstBatch = generator.GenerateMonthlyContracts(monthIndex: 0);
        var secondBatch = generator.GenerateMonthlyContracts(monthIndex: 1);

        AssertBatchContainsAllRequestedCategories(firstBatch, requestedCategories);
        AssertBatchContainsAllRequestedCategories(secondBatch, requestedCategories);
    }

    [Test]
    public void Generator_UsesSmallLargeOrderMixWithDemandBands()
    {
        const int sampleCount = 4000;
        var options = new ContractGeneratorOptions
        {
            MinContractsPerMonth = 1,
            MaxContractsPerMonth = 1,
        };

        var generator = new ContractGenerator(seed: 4499, options: options);
        var smallOrders = 0;
        var largeOrders = 0;

        for (var monthIndex = 0; monthIndex < sampleCount; monthIndex++)
        {
            var contract = generator.GenerateMonthlyContracts(monthIndex)[0];
            var category = contract.Requirements.RequiredCategory;
            Assert.That(category.HasValue, Is.True);

            var categoryProfile = AircraftCategoryCatalog.GetProfileForYear(
                category!.Value,
                ResolveGenerationYear(monthIndex));
            if (contract.QuantityUnits < 100)
            {
                smallOrders++;
                Assert.That(contract.QuantityUnits, Is.InRange(options.SmallOrderMinQuantityUnits, options.SmallOrderMaxQuantityUnits));
                Assert.That(contract.AppliedMarkup, Is.EqualTo(ContractBalance.SmallOrderContractMarkup).Within(1e-9));
                AssertRequirementBand(contract.Requirements, categoryProfile, options, monthIndex, expectLowBand: true);
            }
            else
            {
                largeOrders++;
                Assert.That(contract.QuantityUnits, Is.InRange(options.LargeOrderMinQuantityUnits, options.LargeOrderMaxQuantityUnits));
                Assert.That(contract.AppliedMarkup, Is.EqualTo(ContractBalance.LargeOrderContractMarkup).Within(1e-9));
                AssertRequirementBand(contract.Requirements, categoryProfile, options, monthIndex, expectLowBand: false);
            }
        }

        var observedSmallShare = smallOrders / (double)(smallOrders + largeOrders);
        Assert.That(observedSmallShare, Is.InRange(0.70d, 0.80d));
    }

    [Test]
    public void PricingCalculator_AppliesMarkupByOrderSize()
    {
        var calculator = new ContractPricingCalculator();
        var requirements = new ContractRequirements
        {
            RequiredCategory = AircraftCategory.RegionalJet,
            MinimumReliability = 0.9d,
            MinimumRangeKilometers = 3200d,
            MinimumSpeed = new ContractSpeedRequirement(5000d, 200d),
            MinimumPayloadKilograms = 9000d,
            MinimumCrew = 2,
        };

        var smallQuote = calculator.CalculateQuote(requirements, quantityUnits: 60);
        var largeQuote = calculator.CalculateQuote(requirements, quantityUnits: 200);

        Assert.That(smallQuote.AppliedMarkup, Is.EqualTo(ContractBalance.SmallOrderContractMarkup).Within(1e-9));
        Assert.That(largeQuote.AppliedMarkup, Is.EqualTo(ContractBalance.LargeOrderContractMarkup).Within(1e-9));
        Assert.That(smallQuote.UnitPrice, Is.GreaterThan(smallQuote.EstimatedMinimumUnitCost));
        Assert.That(largeQuote.UnitPrice, Is.GreaterThan(largeQuote.EstimatedMinimumUnitCost));
        Assert.That(smallQuote.TotalPrice, Is.EqualTo(smallQuote.UnitPrice * 60).Within(1d));
        Assert.That(largeQuote.TotalPrice, Is.EqualTo(largeQuote.UnitPrice * 200).Within(1d));
    }

    [Test]
    public void PricingCalculator_AppliesCommercialRunwayPriceMultiplier_ExceptLightSport()
    {
        Assert.That(
            ContractBalance.ResolveCommercialRunwayPriceMultiplier(
                AircraftCategory.RegionalJet,
                PlaneDesignerBalance.Airframe.RunwayMediumLengthMeters),
            Is.EqualTo(ContractBalance.MediumRunwayContractPriceMultiplier).Within(1e-9d));
        Assert.That(
            ContractBalance.ResolveCommercialRunwayPriceMultiplier(
                AircraftCategory.RegionalJet,
                PlaneDesignerBalance.Airframe.RunwayShortLengthMeters),
            Is.EqualTo(ContractBalance.ShortRunwayContractPriceMultiplier).Within(1e-9d));
        Assert.That(
            ContractBalance.ResolveCommercialRunwayPriceMultiplier(
                AircraftCategory.RegionalJet,
                PlaneDesignerBalance.Airframe.RunwayLongLengthMeters),
            Is.EqualTo(1d).Within(1e-9d));
        Assert.That(
            ContractBalance.ResolveCommercialRunwayPriceMultiplier(
                AircraftCategory.LightSportAndRecreational,
                PlaneDesignerBalance.Airframe.RunwayShortLengthMeters),
            Is.EqualTo(1d).Within(1e-9d));
    }

    [Test]
    public void PricingCalculator_AppliesCampaignPriceMultiplierToMilitaryContracts()
    {
        var boostedModifiers = new HistoricalCampaignContractModifiers();
        boostedModifiers.ApplyPriceMultiplier("military", ContractBalance.CampaignPriceVietnamWar);
        var calculator = new ContractPricingCalculator(new StubHistoricalCampaignContractModifierSource(boostedModifiers));

        var requirements = new ContractRequirements
        {
            RequiredCategory = AircraftCategory.MilitaryTactical,
            MinimumReliability = 0.9d,
            MinimumRangeKilometers = 1600d,
            MinimumPayloadKilograms = 2200d,
        };

        var quoted = calculator.CalculateQuote(requirements, quantityUnits: 60);
        var expectedMarkup = ((1d + ContractBalance.SmallOrderContractMarkup) * ContractBalance.CampaignPriceVietnamWar) - 1d;

        Assert.That(quoted.AppliedMarkup, Is.EqualTo(expectedMarkup).Within(1e-9));
        Assert.That(quoted.UnitPrice, Is.GreaterThan(quoted.EstimatedMinimumUnitCost));
    }

    [Test]
    public void Generator_ShiftsTowardMilitaryCategories_WhenCampaignMixIsActive()
    {
        var options = new ContractGeneratorOptions
        {
            MinContractsPerMonth = 1,
            MaxContractsPerMonth = 1,
            RequestedCategories = new[]
            {
                AircraftCategory.LightSportAndRecreational,
                AircraftCategory.MilitaryTactical,
            },
        };

        var modifiers = new HistoricalCampaignContractModifiers();
        modifiers.ApplyCategoryMixMultiplier("civil", ContractBalance.CampaignMixGlobalAviationCrisis);
        modifiers.ApplyCategoryMixMultiplier("military", ContractBalance.CampaignMixVietnamWar);
        var generator = new ContractGenerator(
            seed: 4499,
            options: options,
            campaignModifierSource: new StubHistoricalCampaignContractModifierSource(modifiers));

        var militaryCount = 0;
        var civilCount = 0;
        for (var monthIndex = 0; monthIndex < 24; monthIndex++)
        {
            var contract = generator.GenerateMonthlyContracts(monthIndex)[0];
            if (ContractBalance.IsMilitaryContractCategory(contract.Requirements.RequiredCategory!.Value))
            {
                militaryCount++;
            }
            else
            {
                civilCount++;
            }
        }

        Assert.That(militaryCount, Is.GreaterThan(civilCount));
    }

    [Test]
    public void Generator_SkipsLockedCategories_WhenCampaignLocksCategory()
    {
        var options = new ContractGeneratorOptions
        {
            MinContractsPerMonth = 1,
            MaxContractsPerMonth = 1,
            RequestedCategories = new[]
            {
                AircraftCategory.HeavyTransportWideBody,
                AircraftCategory.RegionalJet,
            },
        };

        var modifiers = new HistoricalCampaignContractModifiers();
        modifiers.LockCategory(AircraftCategory.HeavyTransportWideBody);
        var generator = new ContractGenerator(
            seed: 9931,
            options: options,
            campaignModifierSource: new StubHistoricalCampaignContractModifierSource(modifiers));

        for (var monthIndex = 0; monthIndex < 12; monthIndex++)
        {
            var contract = generator.GenerateMonthlyContracts(monthIndex)[0];
            Assert.That(contract.Requirements.RequiredCategory, Is.EqualTo(AircraftCategory.RegionalJet));
        }
    }

    [Test]
    public void Generator_BlocksWideBodyBeforeIntroductionYear_AndAllowsItAfterward()
    {
        var options = new ContractGeneratorOptions
        {
            MinContractsPerMonth = 1,
            MaxContractsPerMonth = 1,
            RequestedCategories = new[]
            {
                AircraftCategory.HeavyTransportWideBody,
                AircraftCategory.RegionalJet,
            },
        };

        var generator = new ContractGenerator(seed: 6173, options: options);
        var introMonthIndex = (ContractBalance.HeavyTransportWideBodyIntroductionYear - CoreBalance.HistoricalCampaignTechnology.StartYear) * 12;

        var preIntroMonthIndices = new[] { 0, 12, 60, introMonthIndex - 1 };
        for (var index = 0; index < preIntroMonthIndices.Length; index++)
        {
            var contract = generator.GenerateMonthlyContracts(preIntroMonthIndices[index])[0];
            Assert.That(contract.Requirements.RequiredCategory, Is.Not.EqualTo(AircraftCategory.HeavyTransportWideBody));
        }

        var foundWideBodyAfterIntro = false;
        for (var monthIndex = introMonthIndex; monthIndex < introMonthIndex + 36; monthIndex++)
        {
            var contract = generator.GenerateMonthlyContracts(monthIndex)[0];
            if (contract.Requirements.RequiredCategory != AircraftCategory.HeavyTransportWideBody)
            {
                continue;
            }

            foundWideBodyAfterIntro = true;
            break;
        }

        Assert.That(foundWideBodyAfterIntro, Is.True);
    }

    [Test]
    public void Generator_AddsFuelConsumptionRequirements_OnlyWhenCampaignParameterIsUnlocked()
    {
        var options = new ContractGeneratorOptions
        {
            MinContractsPerMonth = 3,
            MaxContractsPerMonth = 3,
            RequestedCategories = new[] { AircraftCategory.RegionalJet },
        };

        var lockedModifiers = new HistoricalCampaignContractModifiers();
        var lockedGenerator = new ContractGenerator(
            seed: 5087,
            options: options,
            campaignModifierSource: new StubHistoricalCampaignContractModifierSource(lockedModifiers));

        for (var monthIndex = 0; monthIndex < 8; monthIndex++)
        {
            var lockedContracts = lockedGenerator.GenerateMonthlyContracts(monthIndex);
            for (var contractIndex = 0; contractIndex < lockedContracts.Count; contractIndex++)
            {
                Assert.That(
                    lockedContracts[contractIndex].Requirements.MaximumFuelConsumptionKilogramsPerHour.HasValue,
                    Is.False);
            }
        }

        var unlockedModifiers = new HistoricalCampaignContractModifiers();
        unlockedModifiers.UnlockContractParameter(ContractBalance.HistoricalCampaignContractParameterFuelConsumption);
        var unlockedGenerator = new ContractGenerator(
            seed: 5087,
            options: options,
            campaignModifierSource: new StubHistoricalCampaignContractModifierSource(unlockedModifiers));

        var foundFuelRequirement = false;
        for (var monthIndex = 0; monthIndex < 8 && !foundFuelRequirement; monthIndex++)
        {
            var unlockedContracts = unlockedGenerator.GenerateMonthlyContracts(monthIndex);
            for (var contractIndex = 0; contractIndex < unlockedContracts.Count; contractIndex++)
            {
                var maximumFuelConsumption =
                    unlockedContracts[contractIndex].Requirements.MaximumFuelConsumptionKilogramsPerHour;
                if (!maximumFuelConsumption.HasValue)
                {
                    continue;
                }

                foundFuelRequirement = true;
                var category = unlockedContracts[contractIndex].Requirements.RequiredCategory;
                Assert.That(category.HasValue, Is.True);
                var (minFuel, maxFuel) =
                    ContractBalance.ResolveFuelConsumptionRequirementRangeKilogramsPerHour(category!.Value);
                Assert.That(maximumFuelConsumption.Value, Is.InRange(minFuel, maxFuel));
            }
        }

        Assert.That(foundFuelRequirement, Is.True);
    }

    [Test]
    public void Balance_ResolvesHistoricalCampaignBonusBatchRanges()
    {
        Assert.That(
            ContractBalance.TryResolveHistoricalCampaignBonusBatchRange(
                "contract.event.korea_war.bonus_batch",
                out var koreaMin,
                out var koreaMax),
            Is.True);
        Assert.That(koreaMin, Is.EqualTo(ContractBalance.CampaignBonusBatchKoreaMinContracts));
        Assert.That(koreaMax, Is.EqualTo(ContractBalance.CampaignBonusBatchKoreaMaxContracts));

        Assert.That(
            ContractBalance.TryResolveHistoricalCampaignBonusBatchRange(
                "contract.event.vietnam_war.bonus_batch",
                out var vietnamMin,
                out var vietnamMax),
            Is.True);
        Assert.That(vietnamMin, Is.EqualTo(ContractBalance.CampaignBonusBatchVietnamMinContracts));
        Assert.That(vietnamMax, Is.EqualTo(ContractBalance.CampaignBonusBatchVietnamMaxContracts));
    }

    [Test]
    public void Balance_BuildsBonusBatchCategoryPool_WithMilitaryBias()
    {
        var categories = ContractBalance.BuildBonusBatchRequestedCategories(ContractBalance.CampaignMixVietnamWar);

        var militaryCount = 0;
        var civilCount = 0;
        for (var index = 0; index < categories.Length; index++)
        {
            if (ContractBalance.IsMilitaryContractCategory(categories[index]))
            {
                militaryCount++;
            }
            else
            {
                civilCount++;
            }
        }

        var expectedMilitaryCopies = (int)System.Math.Round(
            ContractBalance.CampaignBonusBatchMilitaryBaseCopies * ContractBalance.CampaignMixVietnamWar,
            System.MidpointRounding.AwayFromZero);
        expectedMilitaryCopies = System.Math.Max(1, expectedMilitaryCopies);
        expectedMilitaryCopies = System.Math.Min(ContractBalance.CampaignBonusBatchMilitaryMaxCopies, expectedMilitaryCopies);
        var expectedMilitaryCount = expectedMilitaryCopies * ContractBalance.MilitaryRequestedCategories.Length;
        var expectedCivilCount = ContractBalance.CampaignBonusBatchCivilCopies * ContractBalance.CivilRequestedCategories.Length;

        Assert.That(militaryCount, Is.EqualTo(expectedMilitaryCount));
        Assert.That(civilCount, Is.EqualTo(expectedCivilCount));
        Assert.That(militaryCount, Is.GreaterThan(civilCount));
    }

    [Test]
    public void Balance_ResolvesHistoricalRequirementDifficultyMultiplier_WithClampedYearsAndOffset()
    {
        var startYearMultiplier = ContractBalance.ResolveHistoricalCampaignRequirementDifficultyMultiplier(1946);
        var endYearMultiplier = ContractBalance.ResolveHistoricalCampaignRequirementDifficultyMultiplier(2020);
        var clampedBeforeStart = ContractBalance.ResolveHistoricalCampaignRequirementDifficultyMultiplier(1900);
        var shiftedByOffset = ContractBalance.ResolveHistoricalCampaignRequirementDifficultyMultiplier(1950, playerYearOffset: -4);
        var interpolated = ContractBalance.ResolveHistoricalCampaignRequirementDifficultyMultiplier(1960);
        var expectedInterpolated = 0.60d + (((1960 - 1946) / (double)(2020 - 1946)) * 0.40d);

        Assert.That(startYearMultiplier, Is.EqualTo(0.60d).Within(1e-9d));
        Assert.That(endYearMultiplier, Is.EqualTo(1.00d).Within(1e-9d));
        Assert.That(clampedBeforeStart, Is.EqualTo(0.60d).Within(1e-9d));
        Assert.That(shiftedByOffset, Is.EqualTo(0.60d).Within(1e-9d));
        Assert.That(interpolated, Is.EqualTo(expectedInterpolated).Within(1e-9d));
    }

    [Test]
    public void Generator_IgnoresObsoleteRequirementDifficultyMultiplier_WhenUsingYearAdjustedMinimums()
    {
        const double requirementDifficultyMultiplier = 0.60d;
        var options = new ContractGeneratorOptions
        {
            MinContractsPerMonth = 1,
            MaxContractsPerMonth = 1,
            RequestedCategories = new[] { AircraftCategory.RegionalJet },
        };

        var neutralModifiers = new HistoricalCampaignContractModifiers();
        neutralModifiers.SetContractRequirementDifficultyMultiplier(1d);
        var reducedRequirementModifiers = new HistoricalCampaignContractModifiers();
        reducedRequirementModifiers.SetContractRequirementDifficultyMultiplier(requirementDifficultyMultiplier);

        var baselineGenerator = new ContractGenerator(
            seed: 9157,
            options: options,
            campaignModifierSource: new StubHistoricalCampaignContractModifierSource(neutralModifiers));
        var reducedGenerator = new ContractGenerator(
            seed: 9157,
            options: options,
            campaignModifierSource: new StubHistoricalCampaignContractModifierSource(reducedRequirementModifiers));

        for (var monthIndex = 0; monthIndex < 24; monthIndex++)
        {
            var baseline = baselineGenerator.GenerateMonthlyContracts(monthIndex)[0].Requirements;
            var reduced = reducedGenerator.GenerateMonthlyContracts(monthIndex)[0].Requirements;

            Assert.That(reduced.MinimumReliability, Is.EqualTo(baseline.MinimumReliability));
            Assert.That(reduced.MinimumRangeKilometers, Is.EqualTo(baseline.MinimumRangeKilometers));
            Assert.That(reduced.MinimumSpeed, Is.EqualTo(baseline.MinimumSpeed));
            Assert.That(reduced.MinimumPayloadKilograms, Is.EqualTo(baseline.MinimumPayloadKilograms));
            Assert.That(reduced.MinimumManeuverability, Is.EqualTo(baseline.MinimumManeuverability));
            Assert.That(reduced.MaximumTotalWeightKilograms, Is.EqualTo(baseline.MaximumTotalWeightKilograms));
            Assert.That(reduced.MinimumCrew, Is.EqualTo(baseline.MinimumCrew));
            Assert.That(reduced.MaximumRequiredRunwayLengthMeters, Is.EqualTo(baseline.MaximumRequiredRunwayLengthMeters));
            Assert.That(reduced.MaximumFuelConsumptionKilogramsPerHour, Is.EqualTo(baseline.MaximumFuelConsumptionKilogramsPerHour));
        }
    }

    [Test]
    public void Evaluator_BlocksDelivery_WhenAircraftBelowRequirements()
    {
        var evaluator = new ContractEvaluator();
        var aircraft = CreateAircraft(
            reliability: 0.84d,
            rangeKilometers: 1800d,
            speedAtSeaLevel: 90d,
            speedAtFiveKilometers: 84d,
            speedAtTenKilometers: 78d,
            payloadKilograms: 3100d,
            crewCount: 2);

        var contract = new Contract(
            "ctr.test.blocked",
            "Sky Cargo",
            quantityUnits: 90,
            deadlineMonths: 12,
            new ContractRequirements
            {
                RequiredCategory = AircraftCategory.HeavyTransportWideBody,
                MinimumReliability = 0.91d,
                MaximumTotalWeightKilograms = 3500d,
                MinimumRangeKilometers = 2400d,
                MinimumSpeed = new ContractSpeedRequirement(5000d, 95d),
                MinimumPayloadKilograms = 4200d,
                MinimumCrew = 3,
            },
            createdMonthIndex: 0);

        var evaluation = evaluator.Evaluate(contract, aircraft);

        Assert.That(evaluation.CanDeliver, Is.False);
        Assert.That(evaluation.UnmetRequirements.Count, Is.GreaterThan(0));
    }

    [Test]
    public void Evaluator_BlocksDelivery_WhenRunwayRequirementIsTooShort()
    {
        var evaluator = new ContractEvaluator();
        var aircraft = CreateAircraft(
            reliability: 0.9d,
            rangeKilometers: 2600d,
            speedAtSeaLevel: 110d,
            speedAtFiveKilometers: 104d,
            speedAtTenKilometers: 98d,
            payloadKilograms: 4600d,
            crewCount: 2);

        var contract = new Contract(
            "ctr.test.runway",
            "Runway Cargo",
            quantityUnits: 50,
            deadlineMonths: 12,
            new ContractRequirements
            {
                RequiredCategory = AircraftCategory.BusinessVipJet,
                MinimumReliability = 0.8d,
                MaximumRequiredRunwayLengthMeters = PlaneDesignerBalance.Airframe.RunwayShortLengthMeters,
            },
            createdMonthIndex: 0);

        var evaluation = evaluator.Evaluate(contract, aircraft);

        Assert.That(evaluation.CanDeliver, Is.False);
        Assert.That(evaluation.UnmetRequirements, Has.Some.Contains("Runway"));
    }

    [Test]
    public void Evaluator_BlocksDelivery_WhenFuelConsumptionExceedsMaximum()
    {
        var evaluator = new ContractEvaluator();
        var aircraft = CreateAircraft(
            reliability: 0.9d,
            rangeKilometers: 2600d,
            speedAtSeaLevel: 110d,
            speedAtFiveKilometers: 104d,
            speedAtTenKilometers: 98d,
            payloadKilograms: 4600d,
            crewCount: 2,
            averageCruiseFuelBurnKilogramsPerHour: 900d);

        var contract = new Contract(
            "ctr.test.fuel",
            "Fuel Cargo",
            quantityUnits: 50,
            deadlineMonths: 12,
            new ContractRequirements
            {
                RequiredCategory = AircraftCategory.BusinessVipJet,
                MinimumReliability = 0.8d,
                MaximumFuelConsumptionKilogramsPerHour = 650d,
            },
            createdMonthIndex: 0);

        var evaluation = evaluator.Evaluate(contract, aircraft);

        Assert.That(evaluation.CanDeliver, Is.False);
        Assert.That(evaluation.UnmetRequirements, Has.Some.Contains("Fuel burn"));
    }

    [Test]
    public void Evaluator_TreatsRequiredCategoryAsMarketLabel_WhenExplicitRequirementsPass()
    {

        var evaluator = new ContractEvaluator();
        var aircraft = CreateAircraft(
            reliability: 0.9d,
            rangeKilometers: 2600d,
            speedAtSeaLevel: 110d,
            speedAtFiveKilometers: 104d,
            speedAtTenKilometers: 98d,
            payloadKilograms: 4600d,
            crewCount: 2);

        var contract = new Contract(
            "ctr.test.category",
            "Category Cargo",
            quantityUnits: 50,
            deadlineMonths: 12,
            new ContractRequirements
            {
                RequiredCategory = AircraftCategory.BusinessVipJet,
                MinimumReliability = 0.8d,
            },
            createdMonthIndex: 0);

        var evaluation = evaluator.Evaluate(contract, aircraft);

        Assert.That(evaluation.CanDeliver, Is.True);
        Assert.That(evaluation.UnmetRequirements, Is.Empty);
    }

    [Test]
    public void Evaluator_AllowsDelivery_WhenAircraftQualifiesForRequiredCategory()
    {
        var evaluator = new ContractEvaluator();
        var aircraft = CreateAircraft(
            reliability: 0.9d,
            rangeKilometers: 3000d,
            speedAtSeaLevel: 195d,
            speedAtFiveKilometers: 190d,
            speedAtTenKilometers: 180d,
            payloadKilograms: 5000d,
            crewCount: 2,
            category: AircraftCategory.RegionalJet,
            navigationEquipment: AircraftNavigationEquipment.BasicNavigationSuite,
            hasPassengerCabin: true,
            totalWeightKilograms: 6000d,
            maneuverability: 40d);

        var contract = new Contract(
            "ctr.test.qualified.category",
            "VIP Shuttle",
            quantityUnits: 20,
            deadlineMonths: 12,
            new ContractRequirements
            {
                RequiredCategory = AircraftCategory.BusinessVipJet,
                MinimumReliability = 0.8d,
                MinimumRangeKilometers = 2500d,
            },
            createdMonthIndex: 0);

        var evaluation = evaluator.Evaluate(contract, aircraft);

        Assert.That(evaluation.CanDeliver, Is.True);
    }

    [Test]
    public void Board_DeliversContract_WhenAircraftMeetsAllRequirements()
    {
        var deliverableContract = new Contract(
            "ctr.test.ok",
            "Atlas Freight",
            quantityUnits: 40,
            deadlineMonths: 18,
            new ContractRequirements
            {
                MinimumReliability = 0.80d,
                MinimumRangeKilometers = 1500d,
                MinimumSpeed = new ContractSpeedRequirement(0d, 75d),
                MinimumPayloadKilograms = 2500d,
                MinimumCrew = 2,
            },
            createdMonthIndex: 0);

        var board = new ContractBoard(
            new StubGenerator(deliverableContract),
            new ContractEvaluator());

        var generated = board.GenerateContractsForNextMonth();
        var aircraft = CreateAircraft(
            reliability: 0.9d,
            rangeKilometers: 2600d,
            speedAtSeaLevel: 110d,
            speedAtFiveKilometers: 104d,
            speedAtTenKilometers: 98d,
            payloadKilograms: 4600d,
            crewCount: 3);

        var result = board.TryDeliver(generated[0].Id, aircraft);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(board.OpenContracts.Count, Is.EqualTo(0));
        Assert.That(board.DeliveredContracts.Count, Is.EqualTo(1));
        Assert.That(board.DeliveredContracts[0].Status, Is.EqualTo(ContractStatus.Delivered));
    }

    [Test]
    public void Board_InjectsGeneratedContractsForCurrentMonth_WhenContractsAreValid()
    {
        var board = new ContractBoard(new StubGenerator(), new ContractEvaluator());
        _ = board.AdvanceMonthAndGenerateContracts();

        var injectedContract = new Contract(
            "ctr.injected.0001",
            "Bonus Logistics",
            quantityUnits: 40,
            deadlineMonths: 14,
            new ContractRequirements
            {
                RequiredCategory = AircraftCategory.MilitaryTactical,
                MinimumReliability = 0.8d,
            },
            createdMonthIndex: board.CurrentMonthIndex);

        var injectedCount = board.InjectGeneratedContractsForCurrentMonth(new[] { injectedContract });

        Assert.That(injectedCount, Is.EqualTo(1));
        Assert.That(board.OpenContracts, Has.Count.EqualTo(1));
        Assert.That(board.OpenContracts[0].Id, Is.EqualTo("ctr.injected.0001"));
    }

    [Test]
    public void Board_RejectsInjectedContracts_WhenCreatedMonthDoesNotMatch()
    {
        var board = new ContractBoard(new StubGenerator(), new ContractEvaluator());
        _ = board.AdvanceMonthAndGenerateContracts();

        var wrongMonthContract = new Contract(
            "ctr.injected.wrong-month",
            "Bonus Logistics",
            quantityUnits: 40,
            deadlineMonths: 14,
            new ContractRequirements
            {
                RequiredCategory = AircraftCategory.MilitaryTactical,
                MinimumReliability = 0.8d,
            },
            createdMonthIndex: board.CurrentMonthIndex + 1);

        Assert.Throws<System.InvalidOperationException>(() =>
            board.InjectGeneratedContractsForCurrentMonth(new[] { wrongMonthContract }));
    }

    [Test]
    public void DeliveryFlow_ConsumesWarehouseUnits_WhenContractDelivered()
    {
        var deliverableContract = new Contract(
            "ctr.test.stock",
            "Western Air Cargo",
            quantityUnits: 40,
            deadlineMonths: 18,
            new ContractRequirements
            {
                MinimumReliability = 0.75d,
                MinimumRangeKilometers = 1400d,
                MinimumSpeed = new ContractSpeedRequirement(0d, 70d),
                MinimumPayloadKilograms = 2200d,
                MinimumCrew = 2,
            },
            createdMonthIndex: 0);

        var board = new ContractBoard(
            new StubGenerator(deliverableContract),
            new ContractEvaluator());

        var generated = board.GenerateContractsForNextMonth();
        var warehouse = new AircraftProductionFactory();
        warehouse.AddWarehouseUnits("design.stock", 60);

        var aircraft = CreateAircraft(
            reliability: 0.91d,
            rangeKilometers: 2700d,
            speedAtSeaLevel: 112d,
            speedAtFiveKilometers: 105d,
            speedAtTenKilometers: 97d,
            payloadKilograms: 4700d,
            crewCount: 3);

        var catalog = new Dictionary<string, FactoryAircraftTypeCatalogItem>
        {
            ["design.stock"] = new FactoryAircraftTypeCatalogItem(
                "design.stock",
                "Stock Aircraft",
                aircraft,
                requiredFactoryToolLevel: 1,
                requiredLineToolLevel: 1),
        };

        var deliveryService = new ContractWarehouseDeliveryService(board, warehouse);
        var deliveryResult = deliveryService.TryDeliverFromWarehouse(generated[0].Id, "design.stock", catalog);

        Assert.That(deliveryResult.IsSuccess, Is.True);
        Assert.That(warehouse.GetWarehouseUnits("design.stock"), Is.EqualTo(20));
    }

    [Test]
    public void DeliveryFlow_DoesNotDeliver_WhenWarehouseStockIsInsufficient()
    {
        var deliverableContract = new Contract(
            "ctr.test.stock.low",
            "Northern Air Cargo",
            quantityUnits: 55,
            deadlineMonths: 18,
            new ContractRequirements
            {
                MinimumReliability = 0.70d,
                MinimumRangeKilometers = 1200d,
                MinimumSpeed = new ContractSpeedRequirement(0d, 65d),
            },
            createdMonthIndex: 0);

        var board = new ContractBoard(
            new StubGenerator(deliverableContract),
            new ContractEvaluator());

        board.GenerateContractsForNextMonth();

        var warehouse = new AircraftProductionFactory();
        warehouse.AddWarehouseUnits("design.stock", 20);

        var aircraft = CreateAircraft(
            reliability: 0.9d,
            rangeKilometers: 2600d,
            speedAtSeaLevel: 110d,
            speedAtFiveKilometers: 102d,
            speedAtTenKilometers: 96d,
            payloadKilograms: 4300d,
            crewCount: 3);

        var catalog = new Dictionary<string, FactoryAircraftTypeCatalogItem>
        {
            ["design.stock"] = new FactoryAircraftTypeCatalogItem(
                "design.stock",
                "Stock Aircraft",
                aircraft,
                requiredFactoryToolLevel: 1,
                requiredLineToolLevel: 1),
        };

        var deliveryService = new ContractWarehouseDeliveryService(board, warehouse);
        var deliveryResult = deliveryService.TryDeliverFromWarehouse(deliverableContract.Id, "design.stock", catalog);

        Assert.That(deliveryResult.IsSuccess, Is.False);
        Assert.That(board.OpenContracts.Count, Is.EqualTo(1));
        Assert.That(board.DeliveredContracts.Count, Is.EqualTo(0));
        Assert.That(warehouse.GetWarehouseUnits("design.stock"), Is.EqualTo(20));
    }

    [Test]
    public void Board_CancelsContract_WhenDeadlineDropsBelowZero()
    {
        var shortContract = new Contract(
            "ctr.test.expire",
            "North Cargo",
            quantityUnits: 30,
            deadlineMonths: 6,
            new ContractRequirements
            {
                MinimumReliability = 0.7d,
            },
            createdMonthIndex: 0);

        var board = new ContractBoard(
            new StubGenerator(shortContract),
            new ContractEvaluator());

        board.GenerateContractsForNextMonth();

        // After seven monthly advances, remaining months becomes -1 and contract is canceled.
        for (var i = 0; i < 7; i++)
        {
            board.AdvanceMonthAndGenerateContracts();
        }

        Assert.That(shortContract.Status, Is.EqualTo(ContractStatus.Canceled));
        Assert.That(board.CanceledContracts.Count, Is.EqualTo(1));
        Assert.That(board.OpenContracts.Count, Is.EqualTo(0));
    }

    private static void AssertBatchContainsAllRequestedCategories(
        IReadOnlyList<Contract> batch,
        IReadOnlyList<AircraftCategory> requestedCategories)
    {
        var seenCategories = new HashSet<AircraftCategory>();
        for (var index = 0; index < batch.Count; index++)
        {
            var category = batch[index].Requirements.RequiredCategory;
            Assert.That(category.HasValue, Is.True);
            seenCategories.Add(category!.Value);
        }

        Assert.That(seenCategories.Count, Is.EqualTo(requestedCategories.Count));
        for (var index = 0; index < requestedCategories.Count; index++)
        {
            Assert.That(seenCategories.Contains(requestedCategories[index]), Is.True);
        }
    }

    private static int ResolveGenerationYear(int monthIndex)
    {
        if (monthIndex <= 0)
        {
            return CoreBalance.HistoricalCampaignTechnology.StartYear;
        }

        return CoreBalance.HistoricalCampaignTechnology.StartYear + (monthIndex / 12);
    }

    private static void AssertRequirementBand(
        ContractRequirements requirements,
        AircraftCategoryProfile categoryProfile,
        ContractGeneratorOptions options,
        int monthIndex,
        bool expectLowBand)
    {
        if (requirements.MinimumReliability.HasValue)
        {
            var generationYear = ResolveGenerationYear(monthIndex);
            AssertWithinBand(
                requirements.MinimumReliability.Value,
                TypeRequirementBalance.ScaleMinimumReliability(options.MinReliability, generationYear),
                TypeRequirementBalance.ScaleMaximumReliability(options.MaxReliability, generationYear),
                options,
                expectLowBand,
                tolerance: 0.001d);
        }

        if (requirements.MaximumTotalWeightKilograms.HasValue)
        {
            AssertWithinBand(
                requirements.MaximumTotalWeightKilograms.Value,
                categoryProfile.MinMaximumWeightKilograms,
                categoryProfile.MaxMaximumWeightKilograms,
                options,
                expectLowBand,
                tolerance: 0.1d);
        }

        if (requirements.MinimumRangeKilometers.HasValue)
        {
            var (minRange, maxRange) = ResolveBoundedRange(
                categoryProfile.MinRequiredRangeKilometers,
                categoryProfile.MaxRequiredRangeKilometers,
                options.MinRangeKilometers,
                options.MaxRangeKilometers);
            AssertWithinBandOrMinimumFallback(
                requirements.MinimumRangeKilometers.Value,
                minRange,
                maxRange,
                categoryProfile.MinRequiredRangeKilometers,
                options,
                expectLowBand,
                tolerance: 0.1d);
        }

        if (requirements.MinimumPayloadKilograms.HasValue)
        {
            var (minPayload, maxPayload) = ResolveBoundedRange(
                categoryProfile.MinRequiredPayloadKilograms,
                categoryProfile.MaxRequiredPayloadKilograms,
                options.MinPayloadKilograms,
                options.MaxPayloadKilograms);
            AssertWithinBandOrMinimumFallback(
                requirements.MinimumPayloadKilograms.Value,
                minPayload,
                maxPayload,
                categoryProfile.MinRequiredPayloadKilograms,
                options,
                expectLowBand,
                tolerance: 0.1d);
        }

        if (requirements.MinimumManeuverability.HasValue)
        {
            AssertWithinBandOrMinimumFallback(
                requirements.MinimumManeuverability.Value,
                categoryProfile.MinRequiredManeuverability,
                categoryProfile.MaxRequiredManeuverability,
                categoryProfile.MinRequiredManeuverability,
                options,
                expectLowBand,
                tolerance: 0.1d);
        }

        if (requirements.MinimumSpeed.HasValue)
        {
            var (minSpeedKmh, maxSpeedKmh) = ResolveBoundedRange(
                categoryProfile.MinRequiredSpeedKilometersPerHour,
                categoryProfile.MaxRequiredSpeedKilometersPerHour,
                options.MinSpeedMetersPerSecond * 3.6d,
                options.MaxSpeedMetersPerSecond * 3.6d);
            var requiredSpeedKmh = requirements.MinimumSpeed.Value.MinimumSpeedMetersPerSecond * 3.6d;
            AssertWithinBandOrMinimumFallback(
                requiredSpeedKmh,
                minSpeedKmh,
                maxSpeedKmh,
                categoryProfile.MinRequiredSpeedKilometersPerHour,
                options,
                expectLowBand,
                tolerance: 0.4d);
        }

        if (requirements.MinimumCrew.HasValue)
        {
            var minCrew = System.Math.Max(categoryProfile.MinCrew, options.MinCrew);
            var maxCrew = System.Math.Min(categoryProfile.MaxCrew, options.MaxCrew);
            if (minCrew > maxCrew)
            {
                minCrew = categoryProfile.MinCrew;
                maxCrew = categoryProfile.MaxCrew;
            }

            AssertWithinBandOrMinimumFallback(
                requirements.MinimumCrew.Value,
                minCrew,
                maxCrew,
                categoryProfile.MinCrew,
                options,
                expectLowBand);
        }
    }

    private static (double MinValue, double MaxValue) ResolveBoundedRange(
        double categoryMin,
        double categoryMax,
        double optionMin,
        double optionMax)
    {
        var minValue = System.Math.Max(categoryMin, optionMin);
        var maxValue = System.Math.Min(categoryMax, optionMax);
        if (minValue > maxValue)
        {
            minValue = categoryMin;
            maxValue = categoryMax;
        }

        return (minValue, maxValue);
    }

    private static void AssertWithinBand(
        double value,
        double minValue,
        double maxValue,
        ContractGeneratorOptions options,
        bool expectLowBand,
        double tolerance = 0d)
    {
        var (bandMin, bandMax) = GetBandRange(minValue, maxValue, options, expectLowBand);
        Assert.That(value, Is.GreaterThanOrEqualTo(bandMin - tolerance));
        Assert.That(value, Is.LessThanOrEqualTo(bandMax + tolerance));
    }

    private static void AssertWithinBand(
        int value,
        int minValue,
        int maxValue,
        ContractGeneratorOptions options,
        bool expectLowBand)
    {
        var (bandMin, bandMax) = GetBandRange(minValue, maxValue, options, expectLowBand);
        var allowedMin = (int)System.Math.Ceiling(bandMin);
        var allowedMax = (int)System.Math.Floor(bandMax);
        if (allowedMin > allowedMax)
        {
            allowedMin = minValue;
            allowedMax = maxValue;
        }

        Assert.That(value, Is.InRange(allowedMin, allowedMax));
    }

    private static void AssertWithinBandOrMinimumFallback(
        double value,
        double minValue,
        double maxValue,
        double fallbackMinimum,
        ContractGeneratorOptions options,
        bool expectLowBand,
        double tolerance = 0d)
    {
        if (System.Math.Abs(value - fallbackMinimum) <= tolerance)
        {
            return;
        }

        AssertWithinBand(value, minValue, maxValue, options, expectLowBand, tolerance);
    }

    private static void AssertWithinBandOrMinimumFallback(
        int value,
        int minValue,
        int maxValue,
        int fallbackMinimum,
        ContractGeneratorOptions options,
        bool expectLowBand)
    {
        if (value == fallbackMinimum)
        {
            return;
        }

        AssertWithinBand(value, minValue, maxValue, options, expectLowBand);
    }

    private static (double MinValue, double MaxValue) GetBandRange(
        double minValue,
        double maxValue,
        ContractGeneratorOptions options,
        bool expectLowBand)
    {
        var normalizedMin = expectLowBand ? options.LowDemandBandMinNormalized : options.HighDemandBandMinNormalized;
        var normalizedMax = expectLowBand ? options.LowDemandBandMaxNormalized : options.HighDemandBandMaxNormalized;
        var range = maxValue - minValue;
        return (minValue + (range * normalizedMin), minValue + (range * normalizedMax));
    }

    private static AircraftModel CreateAircraft(
        double reliability,
        double rangeKilometers,
        double speedAtSeaLevel,
        double speedAtFiveKilometers,
        double speedAtTenKilometers,
        double payloadKilograms,
        int crewCount,
        double averageCruiseFuelBurnKilogramsPerHour = 0d,
        AircraftCategory category = AircraftCategory.LightMultiRolePiston,
        AircraftConstructionStandard constructionStandard = AircraftConstructionStandard.Civil,
        AircraftNavigationEquipment navigationEquipment = AircraftNavigationEquipment.BasicNavigationSuite,
        bool hasPassengerCabin = false,
        bool hasCargoSpace = false,
        double weaponPayloadKilograms = 0d,
        bool hasSpecialEquipment = false,
        bool hasCombatEquipment = false,
        double totalWeightKilograms = 4200d,
        double maneuverability = 75d)
    {
        var engineDefinition = new EngineDefinition
        {
            Id = "eng.test",
            DisplayName = "Test Engine",
            EngineType = EngineType.Radial,
        };

        var engineParameters = new EngineParameters
        {
            DisplacementLiters = 12d,
            CompressionRatio = 8d,
            BoostType = BoostType.None,
            OptimizationAltitudeMeters = 1800d,
            CoolingEfficiency = 0.8d,
        };

        var engine = new Engine(
            engineDefinition,
            engineParameters,
            new EnginePerformanceReport(
                EngineOutputKind.PowerKilowatts,
                new[]
                {
                    new EngineAltitudePerformance(0d, 620d, 220d),
                    new EngineAltitudePerformance(5000d, 390d, 150d),
                    new EngineAltitudePerformance(10000d, 210d, 86d),
                },
                reliability,
                Mass.FromKilograms(420d),
                cost: 65000d,
                productionManDays: 85d));

        var airframeParameters = new AirframeParameters
        {
            EngineCount = 2,
            StructureType = StructureType.Monocoque,
            MaterialType = MaterialType.Aluminum,
            WingPosition = WingPosition.Low,
            WingShape = WingShape.Straight,
            WingAreaSquareMeters = 36d,
            TailType = TailType.Classic,
            FlapType = FlapType.Fowler,
            LandingGearType = LandingGearType.SemiRetractable,
            EngineCowling = 0.55d,
            FuelAmountLiters = 1400d,
            CrewCount = crewCount,
        };

        var airframe = new Airframe(
            new AirframeDefinition
            {
                Id = "air.test",
                DisplayName = "Test Airframe",
                BaseStructuralWeightKilograms = 1200d,
                ReferenceWingAreaSquareMeters = 36d,
                BaseDragCoefficient = 0.033d,
                BaseLiftCoefficient = 1.1d,
                BaseReliabilityModifier = 1.0d,
                BaseCost = 70000d,
                BaseProductionManDays = 145d,
            },
            airframeParameters,
            new AirframePerformanceReport(
                Mass.FromKilograms(3000d),
                0.05d,
                1.2d,
                maneuverability,
                100000d,
                190d,
                1.0d,
                Force.FromNewtons(110000d),
                Force.FromNewtons(5300d)));

        var family = new AircraftFamilyDefinition
        {
            Id = "fam.test",
            DisplayName = "Test Family",
            EngineDefinitionId = "eng.test",
            AirframeDefinitionId = "air.test",
            LockedEngineType = EngineType.Radial,
            ConstructionStandard = constructionStandard,
            LockedStructureType = StructureType.Monocoque,
            LockedMaterialType = MaterialType.Aluminum,
            LockedEngineCount = 2,
            Category = category,
            BaseEngineParameters = engineParameters.Clone(),
            BaseAirframeParameters = airframeParameters.Clone(),
            BaseDevelopmentCost = 90000d,
            BaseDevelopmentManDays = 220d,
        };

        var type = new AircraftTypeDefinition
        {
            Id = "type.test",
            FamilyId = "fam.test",
            DisplayName = "Test Type",
            NavigationEquipment = navigationEquipment,
            HasPassengerCabin = hasPassengerCabin,
            HasCargoSpace = hasCargoSpace,
            WeaponPayloadKilograms = weaponPayloadKilograms,
            HasSpecialEquipment = hasSpecialEquipment,
            HasCombatEquipment = hasCombatEquipment,
        };

        var performance = new AircraftPerformanceReport(
            new[]
            {
                new AircraftPerformancePoint(0d, speedAtSeaLevel),
                new AircraftPerformancePoint(5000d, speedAtFiveKilometers),
                new AircraftPerformancePoint(10000d, speedAtTenKilometers),
            },
            rangeKilometers,
            Mass.FromKilograms(payloadKilograms),
            220000d,
            320d,
            reliability,
            Mass.FromKilograms(totalWeightKilograms),
            AverageCruiseFuelBurnKilogramsPerHour: averageCruiseFuelBurnKilogramsPerHour);

        return new AircraftModel(
            family,
            type,
            engine,
            airframe,
            performance,
            new DevelopmentCostReport(
                ChangedParameterCount: 0,
                ChangeMagnitudeScore: 0d,
                DevelopmentCost: 90000d,
                DevelopmentManDays: 220d));
    }

    private sealed class StubGenerator : IContractGenerator
    {
        private readonly IReadOnlyList<Contract> _contracts;
        private bool _alreadyGenerated;

        public StubGenerator(params Contract[] contracts)
        {
            _contracts = contracts;
        }

        public IReadOnlyList<Contract> GenerateMonthlyContracts(int monthIndex)
        {
            if (_alreadyGenerated)
            {
                return System.Array.Empty<Contract>();
            }

            _alreadyGenerated = true;
            return _contracts;
        }
    }

    private sealed class StubHistoricalCampaignContractModifierSource : IHistoricalCampaignContractModifierSource
    {
        private readonly HistoricalCampaignContractModifiers _modifiers;

        public StubHistoricalCampaignContractModifierSource(HistoricalCampaignContractModifiers modifiers)
        {
            _modifiers = modifiers;
        }

        public HistoricalCampaignContractModifiers GetCurrentModifiers() => _modifiers;
    }
}



