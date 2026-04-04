namespace DataSetService.FamilyTree;

using System.Text.Json.Serialization;

public sealed record FamilyTreeBuildOptions
{
    public int MaxDepth { get; init; } = 6;
    public int MaxNodes { get; init; } = 400;

    /// <summary>
    /// If true, only expands a child further if the child is a spouse in a family (i.e. has its own family).
    /// This matches the legacy MVC behavior and keeps the tree size manageable.
    /// </summary>
    public bool OnlyExpandChildrenWithFamilies { get; init; } = true;

    /// <summary>
    /// If true, also follows the secondary spouse family (Persons.IsSpouseInFamily2).
    /// The legacy MVC implementation does not, so this is false by default for parity.
    /// </summary>
    public bool IncludeSpouseFamily2 { get; init; } = false;
}

public sealed record FamilyTreeLayoutOptions
{
    public double RowSpacing { get; init; } = 140;
    public double ColSpacing { get; init; } = 220;
    public double SpouseSpacing { get; init; } = 180;

    /// <summary>
    /// Extra horizontal gap between sibling subtrees measured in "slots".
    /// 1 slot = ColSpacing.
    /// </summary>
    public int SiblingGapSlots { get; init; } = 1;
}

public sealed record TreeNodeDto
{
    public required string Id { get; init; }
    public required string Type { get; init; } // "person" | "union"

    [JsonPropertyName("personId")]
    public long? PersonId { get; init; }
    public string? Label { get; init; }
    public string? Photo { get; init; }
    public string? Birthday { get; init; }
    public string? DeathDate { get; init; }

    // Layout (optional, but filled by FamilyTreeService)
    public int? Depth { get; init; }
    public double? X { get; init; }
    public double? Y { get; init; }
}

public sealed record TreeEdgeDto
{
    public required string FromId { get; init; }
    public required string ToId { get; init; }
    public required string Type { get; init; } // "spouse" | "child"
    public string? Label { get; init; }
}

public sealed record FamilyTreeDto
{
    public required IReadOnlyList<TreeNodeDto> Nodes { get; init; }
    public required IReadOnlyList<TreeEdgeDto> Edges { get; init; }
}
