using System.Collections.Generic;
using WrightTycoon.Core.Airframes;

namespace WrightTycoon.Core.Data;

public interface IAirframeDefinitionRepository
{
    AirframeDefinition GetById(string id);

    IReadOnlyCollection<AirframeDefinition> GetAll();
}
