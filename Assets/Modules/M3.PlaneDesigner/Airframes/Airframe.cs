using System;

namespace WrightTycoon.Core.Airframes;

public sealed class Airframe
{
    public Airframe(AirframeDefinition definition, AirframeParameters parameters, AirframePerformanceReport performance)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Parameters = parameters?.Clone() ?? throw new ArgumentNullException(nameof(parameters));
        Performance = performance ?? throw new ArgumentNullException(nameof(performance));
    }

    public AirframeDefinition Definition { get; }

    public AirframeParameters Parameters { get; }

    public AirframePerformanceReport Performance { get; }
}
