using System;

namespace WrightTycoon.Core.Engines;

public sealed class Engine
{
    public Engine(EngineDefinition definition, EngineParameters parameters, EnginePerformanceReport performance)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Parameters = parameters?.Clone() ?? throw new ArgumentNullException(nameof(parameters));
        Performance = performance ?? throw new ArgumentNullException(nameof(performance));
    }

    public EngineDefinition Definition { get; }

    public EngineParameters Parameters { get; }

    public EnginePerformanceReport Performance { get; }
}
