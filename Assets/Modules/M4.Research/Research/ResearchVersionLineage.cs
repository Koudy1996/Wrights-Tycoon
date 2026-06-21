using System;
using System.Collections.Generic;

namespace WrightTycoon.Core.Research;

public static class ResearchVersionLineage
{
    public static string ResolveRootSubjectId(
        string subjectId,
        IReadOnlyDictionary<string, string> sourceSubjectIdBySubjectId)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
        {
            return string.Empty;
        }

        if (sourceSubjectIdBySubjectId is null)
        {
            throw new ArgumentNullException(nameof(sourceSubjectIdBySubjectId));
        }

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var currentId = subjectId;
        while (true)
        {
            if (!visited.Add(currentId))
            {
                return subjectId;
            }

            if (!sourceSubjectIdBySubjectId.TryGetValue(currentId, out var sourceId)
                || string.IsNullOrWhiteSpace(sourceId))
            {
                return currentId;
            }

            if (!sourceSubjectIdBySubjectId.ContainsKey(sourceId))
            {
                return subjectId;
            }

            currentId = sourceId;
        }
    }
}
