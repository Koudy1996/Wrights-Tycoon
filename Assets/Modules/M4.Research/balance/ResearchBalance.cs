namespace WrightTycoon.Core.Balance;

// M4 tuning surface: research approval costs, durations and complexity scaling.
public static class ResearchBalance
{
    public static class Engine
    {
        public const double CostMultiplierMin = 3d;
        public const double CostMultiplierMax = 10d;
        public const int TimeMonthsMin = 12;
        public const int TimeMonthsMax = 60;

        public const double ComplexityWeightOutput = 0.38d;
        public const double ComplexityWeightWeight = 0.2d;
        public const double ComplexityWeightCost = 0.22d;
        public const double ComplexityWeightManDays = 0.2d;

        public const double ComplexityTypeBasePiston = 0.12d;
        public const double ComplexityTypeBaseInline = 0.16d;
        public const double ComplexityTypeBaseV = 0.22d;
        public const double ComplexityTypeBaseRadial = 0.3d;
        public const double ComplexityTypeBaseJet = 0.38d;
        public const double ComplexityTypeBaseTurbojet = 0.48d;
        public const double ComplexityTypeBaseTurbofan = 0.62d;

        public const double OutputPowerMinKilowatts = 35d;
        public const double OutputPowerMaxKilowatts = 60000d;
        public const double OutputThrustMinKiloNewtons = 1d;
        public const double OutputThrustMaxKiloNewtons = 550d;
        public const double WeightMinKilograms = 80d;
        public const double WeightMaxKilograms = 9000d;
        public const double CostMin = 25000d;
        public const double CostMax = 35000000d;
        public const double ManDaysMin = 20d;
        public const double ManDaysMax = 2400d;
    }

    public static class Family
    {
        public const double CostMultiplier = 5d;
        public const int TimeMonths = 36;
        public const double ComplexityReference = 0.4d;
    }

    public static class Type
    {
        public const double CostMultiplierMin = 3d;
        public const double CostMultiplierMax = 10d;
        public const int TimeMonthsMin = 12;
        public const int TimeMonthsMax = 60;

        public const double ComplexityWeightMagnitude = 0.5d;
        public const double ComplexityWeightChangedCount = 0.25d;
        public const double ComplexityWeightWeight = 0.15d;
        public const double ComplexityWeightSpeed = 0.1d;

        public const double ChangeMagnitudeMin = 0d;
        public const double ChangeMagnitudeMax = 8d;
        public const double ChangedCountMin = 0d;
        public const double ChangedCountMax = 12d;
        public const double WeightMinKilograms = 700d;
        public const double WeightMaxKilograms = 260000d;
        public const double SpeedMinKilometersPerHour = 110d;
        public const double SpeedMaxKilometersPerHour = 2600d;

        public const double ReliabilityPerPoint = 0.02d;
        public const double ProductionCostReductionPerPoint = 0.01d;
        public const double ProductionManDaysReductionPerPoint = 0.02d;
        public const double ResearchTimeIncreasePerPoint = 0.03d;
        public const double ResearchCostIncreasePerPoint = 0.05d;
        public const double FastDevelopmentTimeReductionPerPoint = 0.05d;
        public const double FastDevelopmentCostIncreasePerPoint = 0.10d;
    }
}
