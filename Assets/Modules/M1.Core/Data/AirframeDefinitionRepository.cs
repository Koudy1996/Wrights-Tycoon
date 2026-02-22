using System;
using System.Collections.Generic;
using System.Linq;
using WrightTycoon.Core.Airframes;

namespace WrightTycoon.Core.Data;

public sealed class AirframeDefinitionRepository : IAirframeDefinitionRepository
{
    private readonly Dictionary<string, AirframeDefinition> _definitionsById;

    public AirframeDefinitionRepository(IEnumerable<AirframeDefinition> definitions)
    {
        if (definitions is null)
        {
            throw new ArgumentNullException(nameof(definitions));
        }

        _definitionsById = new Dictionary<string, AirframeDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
        {
            if (definition is null)
            {
                throw new ArgumentException("Definition list cannot contain null entries.", nameof(definitions));
            }

            definition.Validate();
            if (!_definitionsById.TryAdd(definition.Id, definition))
            {
                throw new ArgumentException($"Duplicate airframe definition id '{definition.Id}'.", nameof(definitions));
            }
        }

        if (_definitionsById.Count == 0)
        {
            throw new ArgumentException("At least one airframe definition is required.", nameof(definitions));
        }
    }

    public AirframeDefinition GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Definition id cannot be empty.", nameof(id));
        }

        if (!_definitionsById.TryGetValue(id, out var definition))
        {
            throw new KeyNotFoundException($"Airframe definition '{id}' was not found.");
        }

        return definition;
    }

    public IReadOnlyCollection<AirframeDefinition> GetAll() => _definitionsById.Values.ToArray();
}
