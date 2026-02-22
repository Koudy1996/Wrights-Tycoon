using WrightTycoon.Core.Physics;

namespace WrightTycoon.Core.Engines;

public interface IEngineCalculator
{
    EnginePerformanceReport Calculate(EngineDefinition definition, EngineParameters parameters, IAtmosphereModel atmosphereModel);
}
