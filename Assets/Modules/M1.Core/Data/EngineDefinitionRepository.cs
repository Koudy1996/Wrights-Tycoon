using System;
using System.Collections.Generic;
using System.Linq;
using WrightTycoon.Core.Engines;

namespace WrightTycoon.Core.Data;

public sealed class EngineDefinitionRepository : IEngineDefinitionRepository
{
    private readonly Dictionary<string, EngineDefinition> _definitionsById;

    public EngineDefinitionRepository(IEnumerable<EngineDefinition> definitions)
    {
        if (definitions is null)
        {
            throw new ArgumentNullException(nameof(definitions));
        }

        _definitionsById = new Dictionary<string, EngineDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
        {
            if (definition is null)
            {
                throw new ArgumentException("Definition list cannot contain null entries.", nameof(definitions));
            }

            definition.Validate();
            if (!_definitionsById.TryAdd(definition.Id, definition))
            {
                throw new ArgumentException($"Duplicate engine definition id '{definition.Id}'.", nameof(definitions));
            }
        }

        if (_definitionsById.Count == 0)
        {
            throw new ArgumentException("At least one engine definition is required.", nameof(definitions));
        }
    }

    public EngineDefinition GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Definition id cannot be empty.", nameof(id));
        }

        if (!_definitionsById.TryGetValue(id, out var definition))
        {
            throw new KeyNotFoundException($"Engine definition '{id}' was not found.");
        }

        return definition;
    }

    public IReadOnlyCollection<EngineDefinition> GetAll() => _definitionsById.Values.ToArray();
}
