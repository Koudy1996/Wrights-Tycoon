using System;
using System.Collections.Generic;
using NUnit.Framework;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.GovernmentContracts;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.Tests.GovernmentContracts;

[TestFixture]
public sealed class GovernmentContractModuleTests
{
    [Test]
    public void ProbabilityCalculator_ComputesProportionalAndCappedBonuses()
    {
        var halfBonus = GovernmentContractProbabilityCalculator.CalculateBonusByExcessRatio(0.25d);
        var fullBonus = GovernmentContractProbabilityCalculator.CalculateBonusByExcessRatio(0.50d);
        var cappedBonus = GovernmentContractProbabilityCalculator.CalculateBonusByExcessRatio(1.50d);

        Assert.That(halfBonus, Is.EqualTo(0.04d).Within(1e-9d));
        Assert.That(fullBonus, Is.EqualTo(0.08d).Within(1e-9d));
        Assert.That(cappedBonus, Is.EqualTo(0.08d).Within(1e-9d));

        var probability = GovernmentContractProbabilityCalculator.CalculateFinalProbability(
            new[]
            {
                new GovernmentContractParameterBonus(GovernmentContractParameter.Range, 100d, 150d, 0.5d, 0.08d),
                new GovernmentContractParameterBonus(GovernmentContractParameter.Payload, 100d, 150d, 0.5d, 0.08d),
            });

        Assert.That(probability, Is.EqualTo(0.66d).Within(1e-9d));
    }

    [Test]
    public void Generator_UsesTopThirdRangesAndDiscreteRunwayForGovernmentRequirements()
    {
        var random = new DeterministicRandomProvider();
        var options = new GovernmentContractGeneratorOptions
        {
            RequestedAircraftTypes = new[] { AircraftCategory.RegionalJet },
            MinReliability = 0.8d,
            MaxReliability = 0.98d,
        };

        var generator = new GovernmentContractGenerator(random, options);
        var generated = generator.Generate(new SimulationDate(2000, 1), contractSequence: 0);
        var generationYear = generated.GeneratedOn.Year;
        var profile = AircraftCategoryCatalog.GetProfileForYear(AircraftCategory.RegionalJet, generationYear);
        var requirements = generated.Requirements;

        AssertThatWithinTopThird(
            requirements.MinimumMaximumSpeedMetersPerSecond,
            profile.MinRequiredSpeedKilometersPerHour / 3.6d,
            profile.MaxRequiredSpeedKilometersPerHour / 3.6d,
            tolerance: 0.1d);
        AssertThatWithinTopThird(
            requirements.MinimumRangeKilometers,
            profile.MinRequiredRangeKilometers,
            profile.MaxRequiredRangeKilometers,
            tolerance: 0.1d);
        AssertThatWithinTopThird(
            requirements.MinimumPayloadKilograms,
            profile.MinRequiredPayloadKilograms,
            profile.MaxRequiredPayloadKilograms,
            tolerance: 0.1d);
        AssertThatWithinTopThird(
            requirements.MinimumManeuverability,
            profile.MinRequiredManeuverability,
            profile.MaxRequiredManeuverability,
            tolerance: 0.1d);
        AssertThatWithinTopThird(
            requirements.MinimumReliability,
            TypeRequirementBalance.ScaleMinimumReliability(options.MinReliability, generationYear),
            TypeRequirementBalance.ScaleMaximumReliability(options.MaxReliability, generationYear),
            tolerance: 0.001d);
        Assert.That(requirements.MinimumCrewSize, Is.InRange(
            (int)System.Math.Ceiling(LowerTopThird(profile.MinCrew, profile.MaxCrew)),
            profile.MaxCrew));
        var allowedRunwayLengths = ContractBalance.ResolveAllowedRunwayLengthMeters(AircraftCategory.RegionalJet);
        Assert.That(allowedRunwayLengths, Has.Member(requirements.MaximumRequiredRunwayLengthMeters));
    }

    [Test]
    public void Generator_CanSelectSecondRunwayOption_ForMultiTierCategories()
    {
        var random = new DeterministicRandomProvider();
        random.EnqueueDoubles(0d, 0d, 0d, 0d, 0d, 0.75d);

        var generator = new GovernmentContractGenerator(
            random,
            new GovernmentContractGeneratorOptions
            {
                RequestedAircraftTypes = new[] { AircraftCategory.RegionalJet },
            });

        var generated = generator.Generate(new SimulationDate(2000, 1), contractSequence: 0);

        Assert.That(
            generated.Requirements.MaximumRequiredRunwayLengthMeters,
            Is.EqualTo(PlaneDesignerBalance.Airframe.RunwayLongLengthMeters).Within(1e-9d));
    }

    [Test]
    public void PricingService_AppliesGovernmentRunwayPriceMultiplier_ExceptLightSport()
    {
        Assert.That(
            GovernmentContractBalance.ResolveRunwayPriceMultiplier(
                AircraftCategory.RegionalJet,
                PlaneDesignerBalance.Airframe.RunwayMediumLengthMeters),
            Is.EqualTo(GovernmentContractBalance.MediumRunwayPriceMultiplier).Within(1e-9d));
        Assert.That(
            GovernmentContractBalance.ResolveRunwayPriceMultiplier(
                AircraftCategory.RegionalJet,
                PlaneDesignerBalance.Airframe.RunwayShortLengthMeters),
            Is.EqualTo(GovernmentContractBalance.ShortRunwayPriceMultiplier).Within(1e-9d));
        Assert.That(
            GovernmentContractBalance.ResolveRunwayPriceMultiplier(
                AircraftCategory.RegionalJet,
                PlaneDesignerBalance.Airframe.RunwayLongLengthMeters),
            Is.EqualTo(1d).Within(1e-9d));
        Assert.That(
            GovernmentContractBalance.ResolveRunwayPriceMultiplier(
                AircraftCategory.LightSportAndRecreational,
                PlaneDesignerBalance.Airframe.RunwayShortLengthMeters),
            Is.EqualTo(1d).Within(1e-9d));
    }

    [Test]
    public void Generator_BlocksWideBodyBeforeIntroductionYear_AndAllowsItAfterward()
    {
        var random = new DeterministicRandomProvider();
        var options = new GovernmentContractGeneratorOptions
        {
            RequestedAircraftTypes = new[]
            {
                AircraftCategory.HeavyTransportWideBody,
                AircraftCategory.RegionalJet,
            },
        };

        var generator = new GovernmentContractGenerator(random, options);

        var beforeIntro = generator.Generate(new SimulationDate(1959, 12), contractSequence: 0);
        Assert.That(beforeIntro.Requirements.RequiredAircraftType, Is.EqualTo(AircraftCategory.RegionalJet));

        var afterIntro = generator.Generate(new SimulationDate(1960, 1), contractSequence: 1);
        Assert.That(afterIntro.Requirements.RequiredAircraftType, Is.EqualTo(AircraftCategory.HeavyTransportWideBody));
    }

    [Test]
    public void Board_GeneratesInJanuary_AndExpiresAfterSixMonths_WhenNotAccepted()
    {
        var random = new DeterministicRandomProvider();
        var generator = new StubGovernmentContractGenerator(CreateRequirements());
        var board = new GovernmentContractBoard(generator, random);

        var january2000 = new SimulationDate(2000, 1);
        board.ProcessMonth(january2000);
        Assert.That(board.ActiveContract, Is.Not.Null);
        Assert.That(generator.GeneratedCount, Is.EqualTo(1));

        for (var offset = 1; offset <= 5; offset++)
        {
            board.ProcessMonth(january2000.AddMonths(offset));
        }

        Assert.That(board.ActiveContract, Is.Not.Null);
        board.ProcessMonth(january2000.AddMonths(6));
        Assert.That(board.ActiveContract, Is.Null);
        Assert.That(board.ExpiredContracts.Count, Is.EqualTo(1));

        board.ProcessMonth(new SimulationDate(2001, 1));
        Assert.That(board.ActiveContract, Is.Not.Null);
        Assert.That(generator.GeneratedCount, Is.EqualTo(2));
    }

    [Test]
    public void Board_RejectsConceptAircraftSubmissions()
    {
        var random = new DeterministicRandomProvider();
        var board = new GovernmentContractBoard(new StubGovernmentContractGenerator(CreateRequirements()), random);
        var january2000 = new SimulationDate(2000, 1);
        board.ProcessMonth(january2000);

        var conceptSubmission = CreateSubmissionCandidate(AircraftLifecycleStage.Concept, multiplier: 1.25d);
        var acceptance = board.TryAcceptActiveContract(january2000, conceptSubmission);

        Assert.That(acceptance.IsAccepted, Is.False);
        Assert.That(board.ActiveContract, Is.Not.Null);
        Assert.That(board.ActiveContract!.Status, Is.EqualTo(GovernmentContractStatus.Available));
    }

    [Test]
    public void Board_AcceptsAndEvaluatesExactlySixMonthsAfterAcceptance()
    {
        var random = new DeterministicRandomProvider();
        random.EnqueueDoubles(0d); // evaluation roll => guaranteed success
        random.EnqueueInts(300); // delivery base units

        var board = new GovernmentContractBoard(new StubGovernmentContractGenerator(CreateRequirements()), random);
        var january2000 = new SimulationDate(2000, 1);
        board.ProcessMonth(january2000);

        var submission = CreateSubmissionCandidate(AircraftLifecycleStage.InDevelopment, multiplier: 1.5d);
        var acceptance = board.TryAcceptActiveContract(january2000, submission);

        Assert.That(acceptance.IsAccepted, Is.True);
        Assert.That(acceptance.UpfrontFundingGranted, Is.EqualTo(submission.Metrics.DevelopmentCost * GovernmentContractBalance.UpfrontFundingShare).Within(1e-9d));
        Assert.That(board.ActiveContract, Is.Not.Null);
        Assert.That(board.ActiveContract!.Status, Is.EqualTo(GovernmentContractStatus.Accepted));

        GovernmentContractMonthlyUpdate lastUpdate = null!;
        for (var offset = 1; offset <= 6; offset++)
        {
            lastUpdate = board.ProcessMonth(january2000.AddMonths(offset));
        }

        Assert.That(lastUpdate.EvaluatedContract, Is.Not.Null);
        Assert.That(lastUpdate.EvaluationResult, Is.Not.Null);
        Assert.That(lastUpdate.EvaluationResult!.IsSuccess, Is.True);
        Assert.That(board.ActiveContract, Is.Null);
        Assert.That(board.EvaluatedContracts.Count, Is.EqualTo(1));
        Assert.That(board.ActiveDeliveryContract, Is.Not.Null);
        Assert.That(board.ActiveDeliveryContract!.EndsOn, Is.EqualTo(january2000.AddMonths(6 + (10 * 12))));
    }

    [Test]
    public void Board_UsesEngineFamilyTypeDevelopmentPriceForUpfrontFunding()
    {
        var random = new DeterministicRandomProvider();
        var board = new GovernmentContractBoard(new StubGovernmentContractGenerator(CreateRequirements()), random);
        var january2000 = new SimulationDate(2000, 1);
        board.ProcessMonth(january2000);

        var baseCandidate = CreateSubmissionCandidate(AircraftLifecycleStage.InDevelopment, multiplier: 1.4d);
        var pricedCandidate = new GovernmentContractSubmissionCandidate(
            baseCandidate.DesignId,
            baseCandidate.DisplayName,
            baseCandidate.LifecycleStage,
            baseCandidate.Metrics,
            new GovernmentContractDevelopmentPriceBreakdown(
                EnginePrice: 120000d,
                FamilyPrice: 80000d,
                TypePrice: 60000d));

        var acceptance = board.TryAcceptActiveContract(january2000, pricedCandidate);
        Assert.That(acceptance.IsAccepted, Is.True);
        Assert.That(
            acceptance.UpfrontFundingGranted,
            Is.EqualTo((120000d + 80000d + 60000d) * GovernmentContractBalance.UpfrontFundingShare).Within(1e-9d));
    }

    [Test]
    public void Delivery_ConsumesWarehouseUnits_AndGrantsCompletionBonus()
    {
        var random = new DeterministicRandomProvider();
        random.EnqueueDoubles(0d); // evaluation success
        random.EnqueueInts(300); // base units, no bonus units at base probability

        var board = new GovernmentContractBoard(new StubGovernmentContractGenerator(CreateRequirements()), random);
        var january2000 = new SimulationDate(2000, 1);
        board.ProcessMonth(january2000);

        var baselineSubmission = CreateSubmissionCandidate(AircraftLifecycleStage.Completed, multiplier: 1.0d);
        var acceptance = board.TryAcceptActiveContract(january2000, baselineSubmission);
        Assert.That(acceptance.IsAccepted, Is.True);

        for (var offset = 1; offset <= 6; offset++)
        {
            board.ProcessMonth(january2000.AddMonths(offset));
        }

        Assert.That(board.ActiveDeliveryContract, Is.Not.Null);
        var deliveryContract = board.ActiveDeliveryContract!;
        Assert.That(deliveryContract.TotalUnitsRequired, Is.EqualTo(300));

        var inventory = new FakeInventoryGateway();
        inventory.SetUnits(baselineSubmission.DesignId, 500);

        var delivery = board.ProcessDeliveryFromInventory(january2000.AddMonths(6), inventory);

        Assert.That(delivery.DeliveredUnitsThisMonth, Is.EqualTo(300));
        Assert.That(delivery.CompletedThisMonth, Is.True);
        Assert.That(delivery.CompletionBonusGranted, Is.EqualTo(deliveryContract.TotalContractValue * GovernmentContractBalance.DeliveryCompletionBonusShare).Within(0.01d));
        Assert.That(inventory.GetAvailableUnits(baselineSubmission.DesignId), Is.EqualTo(200));
        Assert.That(board.ActiveDeliveryContract, Is.Null);
        Assert.That(board.ClosedDeliveryContracts.Count, Is.EqualTo(1));
    }

    [Test]
    public void Delivery_CanBeProcessedManuallyByRequestedUnits_AndBoardStateCanRestore()
    {
        var random = new DeterministicRandomProvider();
        random.EnqueueDoubles(0d); // evaluation success
        random.EnqueueInts(300); // base units

        var board = new GovernmentContractBoard(new StubGovernmentContractGenerator(CreateRequirements()), random);
        var january2000 = new SimulationDate(2000, 1);
        board.ProcessMonth(january2000);

        var submission = CreateSubmissionCandidate(AircraftLifecycleStage.Completed, multiplier: 1.2d);
        var acceptance = board.TryAcceptActiveContract(january2000, submission);
        Assert.That(acceptance.IsAccepted, Is.True);

        for (var offset = 1; offset <= 6; offset++)
        {
            board.ProcessMonth(january2000.AddMonths(offset));
        }

        Assert.That(board.ActiveDeliveryContract, Is.Not.Null);
        var deliveryId = board.ActiveDeliveryContract!.DeliveryId;
        var totalUnitsRequired = board.ActiveDeliveryContract.TotalUnitsRequired;

        var inventory = new FakeInventoryGateway();
        inventory.SetUnits(submission.DesignId, 500);

        var manual = board.DeliverFromInventory(january2000.AddMonths(6), deliveryId, inventory, requestedUnits: 120);
        Assert.That(manual.DeliveredUnitsThisMonth, Is.EqualTo(120));
        Assert.That(manual.TotalDeliveredUnits, Is.EqualTo(120));
        Assert.That(manual.RemainingUnits, Is.EqualTo(totalUnitsRequired - 120));
        Assert.That(board.ActiveDeliveryContracts.Count, Is.EqualTo(1));

        var snapshot = board.CreateSnapshot();
        var restored = new GovernmentContractBoard(new StubGovernmentContractGenerator(CreateRequirements()), new DeterministicRandomProvider());
        restored.RestoreSnapshot(snapshot);

        Assert.That(restored.ActiveContract, Is.Null);
        Assert.That(restored.EvaluatedContracts.Count, Is.EqualTo(1));
        Assert.That(restored.ActiveDeliveryContracts.Count, Is.EqualTo(1));
        Assert.That(restored.ActiveDeliveryContracts[0].DeliveryId, Is.EqualTo(deliveryId));
        Assert.That(restored.ActiveDeliveryContracts[0].DeliveredUnits, Is.EqualTo(120));
        Assert.That(restored.ActiveDeliveryContracts[0].RemainingUnits, Is.EqualTo(totalUnitsRequired - 120));
    }

    private static GovernmentContractRequirements CreateRequirements()
    {
        return new GovernmentContractRequirements
        {
            RequiredAircraftType = AircraftCategory.RegionalJet,
            SpeedAltitudeBand = GovernmentContractSpeedAltitudeBand.Medium,
            MinimumMaximumSpeedMetersPerSecond = 190d,
            MinimumRangeKilometers = 3000d,
            MinimumPayloadKilograms = 10000d,
            MinimumReliability = 0.9d,
            MinimumManeuverability = 35d,
            MinimumCrewSize = 2,
            MaximumRequiredRunwayLengthMeters = ContractBalance.ResolveRequiredRunwayLengthMeters(AircraftCategory.RegionalJet),
        };
    }

    private static GovernmentContractSubmissionCandidate CreateSubmissionCandidate(
        AircraftLifecycleStage stage,
        double multiplier)
    {
        var requirements = CreateRequirements();
        var speed = requirements.MinimumMaximumSpeedMetersPerSecond * multiplier;
        var range = requirements.MinimumRangeKilometers * multiplier;
        var payload = requirements.MinimumPayloadKilograms * multiplier;
        var reliability = System.Math.Min(1d, requirements.MinimumReliability * multiplier);
        var maneuverability = requirements.MinimumManeuverability * multiplier;
        var crew = System.Math.Max(requirements.MinimumCrewSize, (int)System.Math.Ceiling(requirements.MinimumCrewSize * multiplier));
        var runway = requirements.MaximumRequiredRunwayLengthMeters / System.Math.Max(multiplier, 1d);

        var metrics = new GovernmentContractAircraftMetrics(
            requirements.RequiredAircraftType,
            MaximumSpeedLowAltitudeMetersPerSecond: speed * 0.95d,
            MaximumSpeedMediumAltitudeMetersPerSecond: speed,
            MaximumSpeedHighAltitudeMetersPerSecond: speed * 0.9d,
            RangeKilometers: range,
            PayloadKilograms: payload,
            Reliability: reliability,
            Maneuverability: maneuverability,
            CrewSize: crew,
            RequiredRunwayLengthMeters: runway,
            DevelopmentCost: 200000d);

        return new GovernmentContractSubmissionCandidate(
            "design.gov.test",
            "Government Candidate",
            stage,
            metrics);
    }

    private static double LowerTopThird(double minValue, double maxValue) =>
        minValue + ((maxValue - minValue) * GovernmentContractBalance.TopThirdLowerBoundFactor);

    private static void AssertThatWithinTopThird(double actualValue, double minValue, double maxValue, double tolerance)
    {
        var lowerBound = LowerTopThird(minValue, maxValue);
        Assert.That(actualValue, Is.GreaterThanOrEqualTo(lowerBound - tolerance));
        Assert.That(actualValue, Is.LessThanOrEqualTo(maxValue + tolerance));
    }

    private sealed class DeterministicRandomProvider : IGovernmentContractRandomProvider
    {
        private readonly Queue<int> _ints = new();
        private readonly Queue<double> _doubles = new();

        public void EnqueueInts(params int[] values)
        {
            for (var index = 0; index < values.Length; index++)
            {
                _ints.Enqueue(values[index]);
            }
        }

        public void EnqueueDoubles(params double[] values)
        {
            for (var index = 0; index < values.Length; index++)
            {
                _doubles.Enqueue(values[index]);
            }
        }

        public int NextIntInclusive(int minInclusive, int maxInclusive)
        {
            if (maxInclusive < minInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxInclusive));
            }

            if (_ints.Count == 0)
            {
                return minInclusive;
            }

            var next = _ints.Dequeue();
            if (next < minInclusive)
            {
                return minInclusive;
            }

            return next > maxInclusive ? maxInclusive : next;
        }

        public double NextDouble()
        {
            if (_doubles.Count == 0)
            {
                return 0d;
            }

            var next = _doubles.Dequeue();
            if (next < 0d)
            {
                return 0d;
            }

            return next > 1d ? 1d : next;
        }

        public double NextDouble(double minInclusive, double maxExclusive)
        {
            if (maxExclusive < minInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            }

            return minInclusive + (NextDouble() * (maxExclusive - minInclusive));
        }
    }

    private sealed class StubGovernmentContractGenerator : IGovernmentContractGenerator
    {
        private readonly GovernmentContractRequirements _requirements;

        public StubGovernmentContractGenerator(GovernmentContractRequirements requirements)
        {
            _requirements = requirements ?? throw new ArgumentNullException(nameof(requirements));
        }

        public int GeneratedCount { get; private set; }

        public GovernmentContract Generate(SimulationDate generatedOn, int contractSequence)
        {
            var contract = new GovernmentContract(
                $"gov.stub.{contractSequence:0000}",
                generatedOn,
                _requirements);
            GeneratedCount++;
            return contract;
        }
    }

    private sealed class FakeInventoryGateway : IGovernmentContractInventoryGateway
    {
        private readonly Dictionary<string, int> _unitsByDesign = new(StringComparer.Ordinal);

        public int GetAvailableUnits(string designId)
        {
            return _unitsByDesign.TryGetValue(designId, out var units)
                ? units
                : 0;
        }

        public bool TryConsumeUnits(string designId, int units, out string message)
        {
            var available = GetAvailableUnits(designId);
            if (available < units)
            {
                message = $"Insufficient units. Available {available}, requested {units}.";
                return false;
            }

            _unitsByDesign[designId] = available - units;
            message = $"Consumed {units} units.";
            return true;
        }

        public void SetUnits(string designId, int units) => _unitsByDesign[designId] = units;
    }
}




