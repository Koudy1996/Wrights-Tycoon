using System;
using System.Collections.Generic;
using System.Linq;

namespace WrightTycoon.Core.Fleet;

public sealed record SparePartsGroupSnapshot(string AnchorDesignId, string FamilyId, string[] MemberIds);
public sealed record SparePartsGroupRegistrySnapshot(SparePartsGroupSnapshot[] Groups);

/// <summary>Stable first-registration anchors. Engines use every-pair compatibility; airframes explicitly merge by family.</summary>
public sealed class SparePartsGroupRegistry
{
    private readonly List<SparePartsGroupSnapshot> groups = new();

    /// <summary>Airframes share a single pool per family; historical anchors remain members/aliases.</summary>
    public string ResolveFamily(string designId, string familyId)
    {
        RequireId(designId, nameof(designId)); RequireId(familyId, nameof(familyId));
        var registered = groups.FirstOrDefault(group => group.MemberIds.Contains(designId, StringComparer.Ordinal));
        if (registered is not null && registered.FamilyId != familyId)
            throw new ArgumentException("Registered design belongs to another family.", nameof(familyId));
        var matching = groups.Where(group => group.FamilyId == familyId).ToArray();
        if (matching.Length == 0) return Resolve(designId, familyId, (_, _) => 0d);
        var anchor = matching[0];
        var members = matching.SelectMany(group => group.MemberIds).Concat(new[] { designId }).Distinct(StringComparer.Ordinal).ToArray();
        groups[groups.IndexOf(anchor)] = anchor with { MemberIds = members };
        foreach (var merged in matching.Skip(1)) groups.Remove(merged);
        return anchor.AnchorDesignId;
    }

    public void MergeFamilyGroups()
    {
        foreach (var group in groups.ToArray()) ResolveFamily(group.AnchorDesignId, group.FamilyId);
    }

    public string Resolve(string designId, string familyId, Func<string, string, double> difference)
    {
        RequireId(designId, nameof(designId)); RequireId(familyId, nameof(familyId));
        if (difference is null) throw new ArgumentNullException(nameof(difference));
        var existing = groups.FirstOrDefault(group => group.MemberIds.Contains(designId, StringComparer.Ordinal));
        if (existing is not null)
        {
            if (!string.Equals(existing.FamilyId, familyId, StringComparison.Ordinal))
                throw new ArgumentException("Registered design belongs to another family.", nameof(familyId));
            return existing.AnchorDesignId;
        }
        for (var index = 0; index < groups.Count; index++)
        {
            var group = groups[index];
            if (!string.Equals(group.FamilyId, familyId, StringComparison.Ordinal)) continue;
            var compatible = true;
            foreach (var member in group.MemberIds)
            {
                var value = difference(designId, member);
                LifecycleCalculator.RequireFiniteNonNegative(value, nameof(difference));
                if (value > 100d) throw new ArgumentOutOfRangeException(nameof(difference));
                if (value > MachineSimilarityCalculator.MaximumSharedPartsDifferencePercent) compatible = false;
            }
            if (!compatible) continue;
            groups[index] = group with { MemberIds = group.MemberIds.Concat(new[] { designId }).ToArray() };
            return group.AnchorDesignId;
        }
        groups.Add(new SparePartsGroupSnapshot(designId, familyId, new[] { designId }));
        return designId;
    }

    public string? GetGroupId(string designId)
    {
        RequireId(designId, nameof(designId));
        return groups.FirstOrDefault(group => group.MemberIds.Contains(designId, StringComparer.Ordinal))?.AnchorDesignId;
    }

    public IReadOnlyList<string> MemberIds(string groupId)
    {
        RequireId(groupId, nameof(groupId));
        var group = groups.FirstOrDefault(item => string.Equals(item.AnchorDesignId, groupId, StringComparison.Ordinal));
        return group is null ? Array.Empty<string>() : Array.AsReadOnly(group.MemberIds);
    }

    public SparePartsGroupRegistrySnapshot CreateSnapshot() => new(groups.Select(group =>
        group with { MemberIds = (string[])group.MemberIds.Clone() }).ToArray());

    public void RestoreSnapshot(SparePartsGroupRegistrySnapshot snapshot)
    {
        if (snapshot is null || snapshot.Groups is null) throw new ArgumentException("Invalid parts-group snapshot.", nameof(snapshot));
        var restored = new List<SparePartsGroupSnapshot>();
        var members = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in snapshot.Groups)
        {
            if (group is null) throw new ArgumentException("Missing group.", nameof(snapshot));
            RequireId(group.AnchorDesignId, nameof(snapshot)); RequireId(group.FamilyId, nameof(snapshot));
            if (group.MemberIds is null || group.MemberIds.Length == 0 || !string.Equals(group.MemberIds[0], group.AnchorDesignId, StringComparison.Ordinal))
                throw new ArgumentException("Group must start with its original anchor.", nameof(snapshot));
            foreach (var member in group.MemberIds)
            {
                RequireId(member, nameof(snapshot));
                if (!members.Add(member)) throw new ArgumentException("Design belongs to multiple groups.", nameof(snapshot));
            }
            restored.Add(group with { MemberIds = (string[])group.MemberIds.Clone() });
        }
        groups.Clear(); groups.AddRange(restored);
    }

    private static void RequireId(string id, string name)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Identifier must not be empty.", name);
    }
}
