using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Eobim.RevitApi.Framework;

namespace Eobim.RevitApi.MultiStepActions;

public enum Area_GetInternalRoomsOptions 
{
    WholeRoom = 0,
    RoomSegment = 1,
}

public record Area_GetInternalRoomsArgs(
      List<Area> SubdivisibleAreas
    , Room Room
    , Area_GetInternalRoomsOptions GetInternalRoomsOptions
);

public class Area_GetInternalRooms : MultistepObservableAction<Area_GetInternalRoomsArgs, Area_GetInternalRoomsDto, List<ElementId>>
{
    public override void SafelyInitializeInputs(Area_GetInternalRoomsArgs args)
    {
        _dto.SubdivisibleAreas = args.SubdivisibleAreas;
        _dto.Room = args.Room;
        _dto.GetInternalRoomsOptions = args.GetInternalRoomsOptions;
    }

    protected override void SetActions()
    {
        Add(SetResult);
    }

    public void SetResult(List<string> _telemetry) 
    {
    
    }
}

public class Area_GetInternalRoomsDto: Dto
{
    public List<Area> SubdivisibleAreas { get; set; }
    public Room Room { get; set; }
    public Area_GetInternalRoomsOptions GetInternalRoomsOptions { get; set; }
    public List<ElementId> InternalRoomIds { get; set; }
}