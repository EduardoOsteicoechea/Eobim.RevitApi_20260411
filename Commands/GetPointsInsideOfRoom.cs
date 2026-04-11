using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Eobim.RevitApi.Core;

namespace Eobim.RevitApi.Commands;

[Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
public class GetPointsInsideOfRoom : Framework.ExternalCommand<GetPointsInsideOfRoomDto>
{
    protected override void Prepare()
    {
        Add(GetAllRooms);
        Add(GetRoomLines);
        Add(ModelRoomVertexes);
    }

    public void GetAllRooms()
    {
        _dto.Rooms = RevitFilteredElementCollector.AllRooms(_doc!);
    }

    public void GetRoomLines()
    {
        _dto.RoomsBoundaries = RevitRoom.BoundaryOrderedPoints(_dto.Rooms!);
    }

    public void ModelRoomVertexes()
    {
        var allPoints = new List<XYZ>();

        foreach (RoomBoundary roomBoundary in _dto.RoomsBoundaries!)
        {
            foreach (List<XYZ> boundaryOrderedPoints in roomBoundary.BoundaryOrderedPointsList)
            {
                foreach (XYZ boundaryOrderedPoint in boundaryOrderedPoints)
                {
                    allPoints.Add(boundaryOrderedPoint);
                }
            }
        }

        System.Windows.MessageBox.Show($"{nameof(allPoints)}: {allPoints.Count}");

        var uniformPoints = RevitXYZ.UniformDistancePoints(allPoints);

        System.Windows.MessageBox.Show($"{nameof(uniformPoints)}: {uniformPoints.Count}");

        var solids = new List<Solid>();

        foreach (XYZ point in uniformPoints)
        {
            solids.Add(RevitSolid.SphereFromXYZAndRadius(point));
        }

        System.Windows.MessageBox.Show($"{nameof(solids)}: {solids.Count}");

        foreach (Solid solid in solids)
        {
            RevitDirectShape.GenericModelFromSolid(_doc!, solid);
        }
    }
}

public class GetPointsInsideOfRoomDto
{
    public List<Room>? Rooms { get; set; }
    public Solid? Solid { get; set; }
    public DirectShape? Shape { get; set; }
    public List<RoomBoundary>? RoomsBoundaries { get; set; }

}