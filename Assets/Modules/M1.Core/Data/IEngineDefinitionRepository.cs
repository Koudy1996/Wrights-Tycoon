using System.Collections.Generic;
using WrightTycoon.Core.Engines;

namespace WrightTycoon.Core.Data;

public interface IEngineDefinitionRepository
{
    EngineDefinition GetById(string id);

    IReadOnlyCollection<EngineDefinition> GetAll();
}
