using System.Globalization;
using Microsoft.Data.Sqlite;

namespace DataSetService.FamilyTree;

public sealed class FamilyTreeService
{
    private readonly string _dbPath;

    public FamilyTreeService(string dbPath = "/var/lib/FamilyWeb/FamilyDB.sqlite")
    {
        _dbPath = dbPath;
    }

    private SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString());
        conn.Open();
        return conn;
    }

    private sealed record PersonLite(
        long Id,
        string? FirstName,
        string? SurName,
        string Photo,
        long? SpouseFamily1,
        long? SpouseFamily2,
        string? Birthday,
        string? DeathDate);

    private sealed record FamilyMemberRow(string IndividualType, long IndividualId, long? FamilyAncestor);

    private sealed record FamilyInfo(long FamilyId, List<long> ParentIds, long? AncestorParentId, List<long> ChildIds);

    public FamilyTreeDto BuildTree(long rootPersonId, FamilyTreeBuildOptions? buildOptions = null, FamilyTreeLayoutOptions? layoutOptions = null)
    {
        buildOptions ??= new FamilyTreeBuildOptions();
        layoutOptions ??= new FamilyTreeLayoutOptions();

        using var conn = OpenConnection();

        // --- Prepared commands ---
        using var personCmd = conn.CreateCommand();
        personCmd.CommandText = PersonReadDefinition.SelectByIdWithDeathDate;
        var personIdParam = personCmd.CreateParameter();
        personIdParam.ParameterName = "@id";
        personCmd.Parameters.Add(personIdParam);

        using var famCmd = conn.CreateCommand();
        famCmd.CommandText = "SELECT IndividualType, IndividualID, FamilyAncestor FROM \"Families\" WHERE trim(CAST(ID AS TEXT)) = @familyId";
        var famIdParam = famCmd.CreateParameter();
        famIdParam.ParameterName = "@familyId";
        famCmd.Parameters.Add(famIdParam);

        using var parentFamiliesCmd = conn.CreateCommand();
        parentFamiliesCmd.CommandText = "SELECT DISTINCT CAST(trim(CAST(ID AS TEXT)) AS INTEGER) FROM \"Families\" WHERE IndividualType = 'PAR' AND trim(CAST(IndividualID AS TEXT)) = @personId";
        var parentFamiliesPersonIdParam = parentFamiliesCmd.CreateParameter();
        parentFamiliesPersonIdParam.ParameterName = "@personId";
        parentFamiliesCmd.Parameters.Add(parentFamiliesPersonIdParam);

        // --- Local caches ---
        var persons = new Dictionary<long, PersonLite>();
        var families = new Dictionary<long, FamilyInfo>();
        var parentFamiliesByPerson = new Dictionary<long, List<long>>();

        PersonLite? LoadPerson(long id)
        {
            if (persons.TryGetValue(id, out var cached)) return cached;

            personIdParam.Value = id.ToString(CultureInfo.InvariantCulture);
            using var dr = personCmd.ExecuteReader();
            if (!dr.Read()) return null;

            var person = PersonReadDefinition.Map(dr);
            var p = new PersonLite(
                Id: person.Id,
                SurName: person.SurName,
                FirstName: person.FirstName,
                Photo: person.Photo,
                SpouseFamily1: person.IsSpouseInFamily,
                SpouseFamily2: person.IsSpouseInFamily2,
                Birthday: person.Birthday,
                DeathDate: person.DeathDate);

            persons[id] = p;
            return p;
        }

        FamilyInfo LoadFamily(long familyId)
        {
            if (families.TryGetValue(familyId, out var cached)) return cached;

            famIdParam.Value = familyId.ToString(CultureInfo.InvariantCulture);
            using var dr = famCmd.ExecuteReader();

            var parents = new List<long>();
            var children = new List<long>();
            long? ancestorParent = null;

            while (dr.Read())
            {
                var type = dr.IsDBNull(0) ? "" : dr.GetString(0);
                var individualIdStr = dr.IsDBNull(1) ? null : dr.GetValue(1)?.ToString();
                if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(individualIdStr))
                    continue;

                var individualId = Convert.ToInt64(individualIdStr, CultureInfo.InvariantCulture);
                long? ancestor = null;
                if (!dr.IsDBNull(2))
                {
                    var ancStr = dr.GetValue(2)?.ToString();
                    if (!string.IsNullOrWhiteSpace(ancStr)) ancestor = Convert.ToInt64(ancStr, CultureInfo.InvariantCulture);
                }

                if (LoadPerson(individualId) is null)
                    continue;

                if (type == "PAR")
                {
                    parents.Add(individualId);
                    if (ancestor == 1) ancestorParent = individualId;
                }
                else if (type == "CHIL")
                {
                    children.Add(individualId);
                }
            }

            // Stable ordering
            parents = parents.Distinct().OrderBy(id => id).ToList();
            children = children.Distinct().OrderBy(id => id).ToList();

            // Prefer putting the ancestor-parent first for deterministic spouse placement
            if (ancestorParent is not null && parents.Count > 1 && parents[0] != ancestorParent)
            {
                parents.Remove(ancestorParent.Value);
                parents.Insert(0, ancestorParent.Value);
            }

            var info = new FamilyInfo(familyId, parents, ancestorParent, children);
            families[familyId] = info;
            return info;
        }

        List<long> GetParentFamilies(long personId)
        {
            if (parentFamiliesByPerson.TryGetValue(personId, out var cached))
                return cached;

            parentFamiliesPersonIdParam.Value = personId.ToString(CultureInfo.InvariantCulture);
            using var dr = parentFamiliesCmd.ExecuteReader();

            var familyIds = new List<long>();
            while (dr.Read())
            {
                var familyIdValue = dr.GetValue(0)?.ToString();
                if (string.IsNullOrWhiteSpace(familyIdValue))
                    continue;

                familyIds.Add(Convert.ToInt64(familyIdValue, CultureInfo.InvariantCulture));
            }

            familyIds = familyIds.Distinct().OrderBy(id => id).ToList();
            parentFamiliesByPerson[personId] = familyIds;
            return familyIds;
        }

        static string PersonNodeId(long personId) => $"p:{personId}";
        static string UnionNodeId(long familyId) => $"u:{familyId}";

        var nodeBases = new Dictionary<string, (string Type, long? PersonId, string? Label, string? Photo, string? Birthday, string? DeathDate)>();
        var spouseNotesByPerson = new Dictionary<long, List<string>>();
        var edges = new HashSet<(string From, string To, string Type)>();

        static string FormatName(PersonLite p)
        {
            var label = ($"{p.FirstName} {p.SurName}").Trim();
            return string.IsNullOrWhiteSpace(label) ? $"Person {p.Id}" : label;
        }

        static string FormatPartnerLine(PersonLite p, long familyId)
        {
            var parts = new List<string> { $"partner: {FormatName(p)} ({p.Id})", $"fam {familyId}" };
            if (!string.IsNullOrWhiteSpace(p.Birthday)) parts.Add($"* {p.Birthday}");
            if (!string.IsNullOrWhiteSpace(p.DeathDate)) parts.Add($"† {p.DeathDate}");
            return string.Join(" | ", parts);
        }

        string BuildPersonLabel(PersonLite p)
        {
            var baseLabel = FormatName(p);
            if (!spouseNotesByPerson.TryGetValue(p.Id, out var notes) || notes.Count == 0)
                return baseLabel;

            var distinctNotes = notes
                .Where(note => !string.IsNullOrWhiteSpace(note))
                .Distinct()
                .ToList();

            return distinctNotes.Count == 0
                ? baseLabel
                : string.Join("\n", new[] { baseLabel }.Concat(distinctNotes));
        }

        void EnsurePersonNode(long personId)
        {
            var nid = PersonNodeId(personId);
            if (nodeBases.ContainsKey(nid)) return;
            var p = LoadPerson(personId);
            if (p is null) return;

            var label = BuildPersonLabel(p);

            nodeBases[nid] = ("person", p.Id, label, p.Photo, p.Birthday, p.DeathDate);
        }

        bool HasPersonNode(long personId) => nodeBases.ContainsKey(PersonNodeId(personId));

        void EnsureUnionNode(long familyId)
        {
            var nid = UnionNodeId(familyId);
            if (nodeBases.ContainsKey(nid)) return;
            nodeBases[nid] = ("union", null, null, null, null, null);
        }

        var visitedFamilies = new HashSet<long>();
        var expandedPersons = new HashSet<long>();
        var queue = new Queue<(long PersonId, int Depth)>();

        EnsurePersonNode(rootPersonId);
        queue.Enqueue((rootPersonId, 0));
        expandedPersons.Add(rootPersonId);

        while (queue.Count > 0)
        {
            var (personId, depth) = queue.Dequeue();
            if (depth >= buildOptions.MaxDepth) continue;

            var p = LoadPerson(personId);
            if (p is null) continue;

            var spouseFamilies = new List<long>();
            if (p.SpouseFamily1 is not null) spouseFamilies.Add(p.SpouseFamily1.Value);
            if (buildOptions.IncludeSpouseFamily2 && p.SpouseFamily2 is not null && p.SpouseFamily2 != p.SpouseFamily1)
                spouseFamilies.Add(p.SpouseFamily2.Value);
            spouseFamilies.AddRange(GetParentFamilies(personId));
            spouseFamilies = spouseFamilies.Distinct().ToList();

            foreach (var familyId in spouseFamilies)
            {
                if (!visitedFamilies.Add(familyId))
                    continue;

                EnsureUnionNode(familyId);
                var fam = LoadFamily(familyId);

                long anchorParentId = 0;
                if (fam.ParentIds.Contains(rootPersonId))
                    anchorParentId = rootPersonId;
                else if (fam.ParentIds.Contains(personId))
                    anchorParentId = personId;
                else
                {
                    anchorParentId = fam.ParentIds
                        .FirstOrDefault(parentId => nodeBases.ContainsKey(PersonNodeId(parentId)));

                    if (anchorParentId == 0)
                        anchorParentId = fam.AncestorParentId ?? fam.ParentIds.FirstOrDefault();
                }

                var visibleParentIds = anchorParentId == 0
                    ? fam.ParentIds
                    : new List<long> { anchorParentId };

                if (anchorParentId != 0)
                {
                    foreach (var spouseParentId in fam.ParentIds.Where(id => id != anchorParentId))
                    {
                        var spouse = LoadPerson(spouseParentId);
                        if (spouse is null)
                            continue;

                        if (!spouseNotesByPerson.TryGetValue(anchorParentId, out var notes))
                        {
                            notes = new List<string>();
                            spouseNotesByPerson[anchorParentId] = notes;
                        }

                        notes.Add(FormatPartnerLine(spouse, familyId));
                    }
                }

                // Parents/spouses
                foreach (var parentId in visibleParentIds)
                {
                    EnsurePersonNode(parentId);
                    if (!HasPersonNode(parentId))
                        continue;

                    edges.Add((PersonNodeId(parentId), UnionNodeId(familyId), "spouse"));
                }

                // Children
                foreach (var childId in fam.ChildIds)
                {
                    EnsurePersonNode(childId);
                    if (!HasPersonNode(childId))
                        continue;

                    edges.Add((UnionNodeId(familyId), PersonNodeId(childId), "child"));

                    if (nodeBases.Count >= buildOptions.MaxNodes)
                        continue;

                    if (!expandedPersons.Contains(childId))
                    {
                        var child = LoadPerson(childId);
                        var hasFamily = child is not null && (child.SpouseFamily1 is not null || child.SpouseFamily2 is not null);
                        if (!buildOptions.OnlyExpandChildrenWithFamilies || hasFamily)
                        {
                            expandedPersons.Add(childId);
                            queue.Enqueue((childId, depth + 1));
                        }
                    }
                }

                if (nodeBases.Count >= buildOptions.MaxNodes)
                    break;
            }

            if (nodeBases.Count >= buildOptions.MaxNodes)
                break;
        }

        // --- Layout (MVP) ---
        var positions = FamilyTreeLayout.ComputePositions(
            rootPersonId,
            persons,
            families,
            layoutOptions);

        foreach (var personNode in nodeBases.Where(kvp => kvp.Value.Type == "person" && kvp.Value.PersonId is not null).ToList())
        {
            var pid = personNode.Value.PersonId!.Value;
            var person = LoadPerson(pid);
            if (person is null)
                continue;

            nodeBases[personNode.Key] = (
                personNode.Value.Type,
                personNode.Value.PersonId,
                BuildPersonLabel(person),
                personNode.Value.Photo,
                personNode.Value.Birthday,
                personNode.Value.DeathDate);
        }

        var nodes = nodeBases.Select(kvp =>
        {
            var (type, pid, label, photo, birthday, deathDate) = kvp.Value;
            var hasPos = positions.TryGetValue(kvp.Key, out var pos);
            return new TreeNodeDto
            {
                Id = kvp.Key,
                Type = type,
                PersonId = pid,
                Label = label,
                Photo = photo,
                Birthday = birthday,
                DeathDate = deathDate,
                Depth = hasPos ? pos.Depth : null,
                X = hasPos ? pos.X : null,
                Y = hasPos ? pos.Y : null,
            };
        }).OrderBy(n => n.Type).ThenBy(n => n.PersonId ?? long.MaxValue).ThenBy(n => n.Id).ToList();

        var edgeDtos = edges.Select(e => new TreeEdgeDto { FromId = e.From, ToId = e.To, Type = e.Type })
            .OrderBy(e => e.Type)
            .ThenBy(e => e.FromId)
            .ThenBy(e => e.ToId)
            .ToList();

        return new FamilyTreeDto { Nodes = nodes, Edges = edgeDtos };
    }

    private static class FamilyTreeLayout
    {
        internal readonly record struct Pos(double X, double Y, int Depth);

        public static Dictionary<string, Pos> ComputePositions(
            long rootPersonId,
            Dictionary<long, PersonLite> persons,
            Dictionary<long, FamilyInfo> families,
            FamilyTreeLayoutOptions opts)
        {
            static string P(long id) => $"p:{id}";
            static string U(long id) => $"u:{id}";

            var pos = new Dictionary<string, Pos>();

            PersonLite? GetPerson(long id) => persons.TryGetValue(id, out var p) ? p : null;

            long? GetPrimaryFamilyId(long personId)
            {
                var p = GetPerson(personId);
                if (p is null) return null;

                var candidateFamilyIds = families.Values
                    .Where(family => family.ParentIds.Contains(personId))
                    .Select(family => family.FamilyId)
                    .Distinct()
                    .ToList();

                if (candidateFamilyIds.Count == 0)
                {
                    if (p.SpouseFamily1 is not null) return p.SpouseFamily1;
                    return p.SpouseFamily2;
                }

                return candidateFamilyIds
                    .OrderByDescending(familyId => families[familyId].ParentIds.Count)
                    .ThenByDescending(familyId => families[familyId].ChildIds.Count)
                    .ThenBy(familyId => familyId == p.SpouseFamily1 ? 0 : familyId == p.SpouseFamily2 ? 1 : 2)
                    .ThenBy(familyId => familyId)
                    .First();
            }

            int gap = Math.Max(0, opts.SiblingGapSlots);

            int LayoutPerson(long personId, int depth, int xSlotStart)
            {
                var famId = GetPrimaryFamilyId(personId);
                if (famId is not null && families.ContainsKey(famId.Value))
                {
                    return LayoutFamily(famId.Value, depth, xSlotStart);
                }

                var x = (xSlotStart + 0.5) * opts.ColSpacing;
                var y = depth * opts.RowSpacing;
                pos[P(personId)] = new Pos(x, y, depth);
                return 1;
            }

            int LayoutFamily(long familyId, int depth, int xSlotStart)
            {
                if (!families.TryGetValue(familyId, out var fam))
                    return 1;

                // Layout children first to determine required width
                var childWidths = new List<(long ChildId, int WidthSlots, int StartSlot)>();
                int cur = xSlotStart;
                foreach (var childId in fam.ChildIds)
                {
                    int w = LayoutPerson(childId, depth + 1, cur);
                    childWidths.Add((childId, w, cur));
                    cur += w + gap;
                }

                int childrenWidth = childWidths.Count == 0
                    ? 0
                    : (cur - xSlotStart - gap);

                int familyWidth = Math.Max(2, Math.Max(1, childrenWidth));

                // Center family on its child span when possible
                double unionX = (xSlotStart + familyWidth / 2.0) * opts.ColSpacing;
                double unionY = depth * opts.RowSpacing;

                pos[U(familyId)] = new Pos(unionX, unionY, depth);

                // Place parents/spouses around the union
                if (fam.ParentIds.Count == 0)
                {
                    // nothing
                }
                else if (fam.ParentIds.Count == 1)
                {
                    pos[P(fam.ParentIds[0])] = new Pos(unionX, unionY, depth);
                }
                else
                {
                    // Keep deterministic left/right
                    var leftParent = fam.ParentIds[0];
                    var rightParent = fam.ParentIds[1];

                    pos[P(leftParent)] = new Pos(unionX - opts.SpouseSpacing / 2.0, unionY, depth);
                    pos[P(rightParent)] = new Pos(unionX + opts.SpouseSpacing / 2.0, unionY, depth);

                    // Any extra parents (unexpected) are stacked near union
                    for (int i = 2; i < fam.ParentIds.Count; i++)
                    {
                        pos[P(fam.ParentIds[i])] = new Pos(unionX + (i - 1) * 10, unionY, depth);
                    }
                }

                // Ensure at least the root person has a position if they're part of this family
                return familyWidth;
            }

            // Root placement
            var rootPrimaryFam = GetPrimaryFamilyId(rootPersonId);
            if (rootPrimaryFam is not null && families.ContainsKey(rootPrimaryFam.Value))
            {
                LayoutFamily(rootPrimaryFam.Value, depth: 0, xSlotStart: 0);
            }
            else
            {
                LayoutPerson(rootPersonId, depth: 0, xSlotStart: 0);
            }

            return pos;
        }
    }
}
