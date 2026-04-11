using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

namespace Eobim.RevitApi.Core;

public static class RevitRoom
{
    public static List<RoomBoundary> BoundaryOrderedPoints
    (
        List<Room> rooms
    )
    {
        var spatialElementBoundaryOptions = new SpatialElementBoundaryOptions
        {
            StoreFreeBoundaryFaces = false,
            SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish
        };

        var result = new List<RoomBoundary>();

        foreach (Room room in rooms)
        {
            var boundarySegmentsList = room.GetBoundarySegments(spatialElementBoundaryOptions);

            var roomBoundary = new RoomBoundary();

            foreach (List<BoundarySegment> boundarySegments in boundarySegmentsList)
            {
                roomBoundary.BoundarySegments.Add(boundarySegments);

                foreach (BoundarySegment boundarySegment in boundarySegments)
                {
                    var a = boundarySegment.ElementId;
                    roomBoundary.BoundaryOrderedPointsList.Add(
                        boundarySegment
                        .GetCurve()
                        .Tessellate()
                        .ToList()
                    );
                }
            }

            result.Add(roomBoundary);
        }

        return result;
    }
}