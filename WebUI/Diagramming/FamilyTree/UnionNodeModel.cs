using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;

namespace FamilyWebBlazorServer.Diagramming.FamilyTree;

public sealed class UnionNodeModel : NodeModel
{
    public UnionNodeModel(string id, Point position)
        : base(id, position)
    {
        Size = new Size(14, 14);

        AddPort(PortAlignment.Left);
        AddPort(PortAlignment.Right);
        AddPort(PortAlignment.Top);
        AddPort(PortAlignment.Bottom);
    }
}