using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;

namespace FamilyWebBlazorServer.Diagramming.FamilyTree;

public sealed class PersonNodeModel : NodeModel
{
    public PersonNodeModel(string id, Point position, long personId, string label, string? photo)
        : base(id, position)
    {
        PersonId = personId;
        Label = label;
        Photo = photo;

        Size = new Size(180, 70);

        AddPort(PortAlignment.Left);
        AddPort(PortAlignment.Right);
        AddPort(PortAlignment.Top);
        AddPort(PortAlignment.Bottom);
    }

    public long PersonId { get; }
    public string Label { get; }
    public string? Photo { get; }
}