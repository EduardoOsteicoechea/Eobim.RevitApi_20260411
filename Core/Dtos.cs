using Autodesk.Revit.DB;

namespace Eobim.RevitApi.Core;


public class RoomBoundary
{
    public ElementId? RoomElementId { get; set; }
    public List<List<XYZ>> BoundaryOrderedPointsList { get; set; } = [];
    public List<List<BoundarySegment>> BoundarySegments { get; set; } = [];
}