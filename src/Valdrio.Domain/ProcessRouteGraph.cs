using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Valdrio.Domain;

/// <summary>
/// Optional, versioned process drawing. Connections are descriptive metadata,
/// never amounts, drivers or automatically applied allocation keys.
/// Encoded in node tags to preserve the existing model file schema.
/// </summary>
public enum ProcessRouteKind { Sequence, AuxiliaryService }
public enum ProcessRouteRestriction
{
    None,
    MissingNode,
    SequenceNeedsPrincipals,
    ServiceNeedsAuxiliary,
    PrincipalSelfLoop,
    SequenceCycle
}
public sealed record ProcessRouteLink(EntityId Source, EntityId Destination, ProcessRouteKind Kind);
public sealed record ProcessRouteValidation(
    IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings, int LinkCount)
{
    public bool IsValid => Errors.Count == 0;
}
public sealed record ProcessRoutePoint(int X, int Y);

public static class ProcessRouteGraph
{
    public const string SequencePrefix = "valdrio:route:sequence:";
    public const string ServicePrefix = "valdrio:route:service:";
    public const string PositionPrefix = "valdrio:route:position:";
    public const string ValidationPrefix = "valdrio:route:validated:";

    public static bool IsTag(string tag) => tag.StartsWith("valdrio:route:", StringComparison.Ordinal);

    private static bool TryDecode(string tag, out ProcessRouteKind kind, out EntityId destination)
    {
        kind = ProcessRouteKind.Sequence;
        destination = default;
        string value;
        if (tag.StartsWith(SequencePrefix, StringComparison.Ordinal))
            value = tag[SequencePrefix.Length..];
        else if (tag.StartsWith(ServicePrefix, StringComparison.Ordinal))
        {
            kind = ProcessRouteKind.AuxiliaryService;
            value = tag[ServicePrefix.Length..];
        }
        else return false;
        return Guid.TryParseExact(value, "D", out var id) && id != Guid.Empty &&
            (destination = new EntityId(id)).IsValid;
    }

    public static IReadOnlyList<ProcessRouteLink> Read(IReadOnlyList<CalculationNodeDefinition> nodes) =>
        nodes.SelectMany(n => n.FunctionTags
            .Where(t => t.StartsWith(SequencePrefix, StringComparison.Ordinal) ||
                        t.StartsWith(ServicePrefix, StringComparison.Ordinal))
            .Select(t => TryDecode(t, out var kind, out var dest)
                ? new ProcessRouteLink(n.Id, dest, kind)
                : null)
            .OfType<ProcessRouteLink>())
        .Distinct().OrderBy(x => x.Source).ThenBy(x => x.Kind).ThenBy(x => x.Destination).ToArray();

    /// <summary>
    /// Descriptive route restrictions, independently of any cost-allocation rule.
    /// Auxiliary self service is permitted; a principal cannot be its own next step.
    /// A new operational edge must not introduce a directed process cycle.
    /// </summary>
    public static ProcessRouteRestriction Restriction(
        IReadOnlyList<CalculationNodeDefinition> nodes, ProcessRouteLink link)
    {
        var source = nodes.FirstOrDefault(n => n.Id == link.Source);
        var target = nodes.FirstOrDefault(n => n.Id == link.Destination);
        if (source is null || target is null || !source.IsActive || !target.IsActive)
            return ProcessRouteRestriction.MissingNode;

        if (source.Role == NodeRole.Auxiliary)
            return link.Kind == ProcessRouteKind.AuxiliaryService
                ? ProcessRouteRestriction.None
                : ProcessRouteRestriction.SequenceNeedsPrincipals;

        if (link.Kind != ProcessRouteKind.Sequence)
            return ProcessRouteRestriction.ServiceNeedsAuxiliary;
        if (target.Role != NodeRole.Principal)
            return ProcessRouteRestriction.SequenceNeedsPrincipals;
        if (source.Id == target.Id)
            return ProcessRouteRestriction.PrincipalSelfLoop;

        var adjacency = Read(nodes).Where(l => l.Kind == ProcessRouteKind.Sequence)
            .GroupBy(l => l.Source)
            .ToDictionary(g => g.Key, g => g.Select(l => l.Destination).ToArray());
        var visited = new HashSet<EntityId>();
        var pending = new Stack<EntityId>();
        pending.Push(target.Id);
        while (pending.Count > 0)
        {
            var next = pending.Pop();
            if (next == source.Id) return ProcessRouteRestriction.SequenceCycle;
            if (!visited.Add(next)) continue;
            if (adjacency.TryGetValue(next, out var successors))
                foreach (var successor in successors) pending.Push(successor);
        }
        return ProcessRouteRestriction.None;
    }

    private static IReadOnlyList<string> ReplaceTags(IEnumerable<string> tags, Func<string, bool> remove,
        IEnumerable<string> append) => tags.Where(t => !remove(t)).Concat(append)
        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

    public static CalculationNodeDefinition WithLink(CalculationNodeDefinition source,
        ProcessRouteLink link, bool enabled)
    {
        if (source.Id != link.Source) throw new ArgumentException("Link source differs from edited node.");
        var prefix = link.Kind == ProcessRouteKind.Sequence ? SequencePrefix : ServicePrefix;
        var tag = prefix + link.Destination;
        var tags = ReplaceTags(source.FunctionTags, t => t == tag,
            enabled ? [tag] : []);
        return Copy(source, tags);
    }

    public static CalculationNodeDefinition WithPosition(CalculationNodeDefinition source,
        ProcessRoutePoint? point)
    {
        var tags = ReplaceTags(source.FunctionTags,
            t => t.StartsWith(PositionPrefix, StringComparison.Ordinal),
            point is null ? [] :
            [PositionPrefix + point.X.ToString(CultureInfo.InvariantCulture) +
             ":" + point.Y.ToString(CultureInfo.InvariantCulture)]);
        return Copy(source, tags);
    }

    public static ProcessRoutePoint? ReadPosition(CalculationNodeDefinition node)
    {
        var value = node.FunctionTags.FirstOrDefault(t => t.StartsWith(PositionPrefix, StringComparison.Ordinal));
        if (value is null) return null;
        var coords = value[PositionPrefix.Length..].Split(':');
        if (coords.Length != 2 ||
            !int.TryParse(coords[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ||
            !int.TryParse(coords[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
            return null;
        return new ProcessRoutePoint(x, y);
    }

    public static ProcessRouteValidation Check(IReadOnlyList<CalculationNodeDefinition> nodes)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var byId = nodes.ToDictionary(n => n.Id);
        var links = Read(nodes);
        foreach (var source in nodes)
            foreach (var tag in source.FunctionTags)
                if ((tag.StartsWith(SequencePrefix, StringComparison.Ordinal) ||
                     tag.StartsWith(ServicePrefix, StringComparison.Ordinal)) &&
                     !TryDecode(tag, out _, out _))
                    errors.Add("Invalid route reference in node " + source.Code);

        foreach (var link in links)
        {
            if (!byId.TryGetValue(link.Source, out var source) ||
                !byId.TryGetValue(link.Destination, out var target) ||
                !source.IsActive || !target.IsActive)
            {
                errors.Add("Missing/inactive destination in a route from " +
                    (source?.Code ?? link.Source.ToString()));
                continue;
            }
            if (link.Kind == ProcessRouteKind.Sequence &&
                (source.Role != NodeRole.Principal || target.Role != NodeRole.Principal ||
                 source.Id == target.Id))
                errors.Add("Operational sequence must link two different principal nodes: " + source.Code);
            if (link.Kind == ProcessRouteKind.AuxiliaryService && source.Role != NodeRole.Auxiliary)
                errors.Add("Auxiliary service must originate in an auxiliary node: " + source.Code);
        }
        var sequence = links.Where(l => l.Kind == ProcessRouteKind.Sequence)
            .GroupBy(l => l.Source).ToDictionary(g => g.Key, g => g.Select(l => l.Destination).ToArray());
        var visited = new HashSet<EntityId>();
        var active = new HashSet<EntityId>();
        bool Visit(EntityId id)
        {
            if (active.Contains(id)) return false;
            if (!visited.Add(id)) return true;
            active.Add(id);
            foreach (var next in sequence.GetValueOrDefault(id) ?? [])
                if (!Visit(next)) return false;
            active.Remove(id);
            return true;
        }
        if (sequence.Keys.Any(id => !Visit(id)))
            errors.Add("An operational sequence contains a cycle.");
        if (links.Count == 0)
            errors.Add("Draw at least one relationship before validating the process map.");
        foreach (var node in nodes.Where(n => n.IsActive &&
            !links.Any(l => l.Source == n.Id || l.Destination == n.Id)))
            warnings.Add("Node has no drawn relationships: " + node.Code);
        return new(errors.Distinct(StringComparer.Ordinal).ToArray(),
            warnings.Distinct(StringComparer.Ordinal).ToArray(), links.Count);
    }

    public static string Fingerprint(IReadOnlyList<CalculationNodeDefinition> nodes)
    {
        var identity = string.Join("|", nodes.OrderBy(n => n.Id).Select(n =>
            n.Id + ":" + n.Role + ":" + NodeCostClassification.Function(n) + ":" + n.IsActive));
        var routes = string.Join("|", Read(nodes).Select(l =>
            l.Source + ">" + l.Destination + ":" + l.Kind));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity + "|" + routes)))
            .ToLowerInvariant();
    }

    public static bool IsValidated(IReadOnlyList<CalculationNodeDefinition> nodes)
    {
        if (nodes.Count == 0 || !Check(nodes).IsValid) return false;
        var marker = ValidationPrefix + Fingerprint(nodes);
        return nodes.OrderBy(n => n.Id).First().FunctionTags.Contains(marker, StringComparer.Ordinal);
    }

    public static IReadOnlyList<CalculationNodeDefinition> MarkValidated(
        IReadOnlyList<CalculationNodeDefinition> nodes)
    {
        var check = Check(nodes);
        if (!check.IsValid) throw new ArgumentException(string.Join("; ", check.Errors));
        var first = nodes.OrderBy(n => n.Id).First();
        var updated = Copy(first, ReplaceTags(first.FunctionTags,
            t => t.StartsWith(ValidationPrefix, StringComparison.Ordinal),
            [ValidationPrefix + Fingerprint(nodes)]));
        return nodes.Select(n => n.Id == first.Id ? updated : n).ToArray();
    }

    // When cloning a model, route targets must follow the new node identities.
    public static IReadOnlyList<string> RemapTags(IEnumerable<string> original,
        IReadOnlyDictionary<EntityId, EntityId> remap)
    {
        var result = new List<string>();
        foreach (var tag in original)
        {
            if (tag.StartsWith(ValidationPrefix, StringComparison.Ordinal)) continue;
            if (TryDecode(tag, out var kind, out var oldDest))
            {
                if (remap.TryGetValue(oldDest, out var newDest))
                    result.Add((kind == ProcessRouteKind.Sequence ? SequencePrefix : ServicePrefix) + newDest);
            }
            else if (!tag.StartsWith(SequencePrefix, StringComparison.Ordinal) &&
                     !tag.StartsWith(ServicePrefix, StringComparison.Ordinal))
                result.Add(tag);
        }
        return result.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    private static CalculationNodeDefinition Copy(
        CalculationNodeDefinition node, IReadOnlyList<string> tags) =>
        new(node.Id, node.ModelVersionId, node.PhysicalCenterId,
            node.Code, node.Role, tags, node.IsActive);
}
