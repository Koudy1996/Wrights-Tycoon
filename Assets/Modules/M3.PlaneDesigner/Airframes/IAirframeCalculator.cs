namespace WrightTycoon.Core.Airframes;

public interface IAirframeCalculator
{
    AirframePerformanceReport Calculate(AirframeDefinition definition, AirframeParameters parameters);
}
