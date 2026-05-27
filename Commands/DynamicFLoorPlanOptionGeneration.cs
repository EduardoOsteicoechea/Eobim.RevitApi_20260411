using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Eobim.RevitApi.Framework;
using Eobim.RevitApi.MultiStepActions;
using Eobim.RevitApi.MultiStepActions.Contour;
using Eobim.RevitApi.MultiStepActions.Face;
using Eobim.RevitApi.MultiStepActions.Grid;
using Eobim.RevitApi.SelectionFilter;
using UIFramework;

namespace Eobim.RevitApi.Commands;


[Transaction(TransactionMode.Manual)]
public class DynamicFLoorPlanOptionGeneration : ExternalCommand<object, DynamicFLoorPlanOptionGenerationDto, bool>
{
    protected override void SetActions()
    {
        Add(GetAllColumns);
        Add(GetSubdivisibleAreas);
        Add(PromptUserToPickRoom);
        Add(ValidateIfSelectedRoomIsInsideOfSubdivisibleArea);
        Add(InterruptWorkflowIfRoomIsNotInsideAnySubdivisibleArea);
        Add(GetContainingSubdivisibleArea);
        Add(GetContainingSubdivisibleAreaBoundarySegments);
        Add(GetContainingSubdivisibleAreaBottomFace);
        Add(GenerateContainingAreaGridLines);
        Add(GetRoomBoundarySegments);
        Add(GetRoomBottomFace);
        Add(GenerateContainingAreaGridRemainingGridOutsideRoom);
        Add(SetResult);

    }

    public void GetAllColumns(List<string> _telemetry)
    {
        var result = new FilteredElementCollector(_doc!)
            .WhereElementIsNotElementType()
            .OfCategory(BuiltInCategory.OST_StructuralColumns)
            .ToElements()
            .ToList();

        if (result is null)
        {
            throw new ArgumentException("No columns found.");
        }
        else
        {
            _telemetry.Add($"{result.Count} columns found.");

            _dto.Columns = result;
        }
    }

    public void GetSubdivisibleAreas(List<string> _telemetry)
    {
        var result = new FilteredElementCollector(_doc!)
            .WhereElementIsNotElementType()
            .OfCategory(BuiltInCategory.OST_Areas)
            .Where(a => a.Name.Contains("subdivisible"))
            .Cast<Area>()
            .ToList();

        if (result is null)
        {
            throw new ArgumentException("No subdivisible areas found.");
        }
        else
        {
            _telemetry.Add($"{result.Count} subdivisible areas found.");
            _dto.SubdivisibleAreas = result;
        }
    }

    public void PromptUserToPickRoom(List<string> _telemetry)
    {
        var uidoc = _commandData.Application.ActiveUIDocument;

        var selectionFilter = new RoomSelectionFilter();

        var pickedReference = uidoc.Selection.PickObject(ObjectType.Element, selectionFilter, "Select a room");

        var room = uidoc.Document.GetElement(pickedReference) as Room;

        if (room is null)
        {
            throw new InvalidOperationException("Selected element is not a room.");
        }
        else
        {
            _telemetry.Add($"User selected room: {room.Name} (ID: {room.Id}).");

            _dto.SelectedRoom = room;
        }
    }

    public void ValidateIfSelectedRoomIsInsideOfSubdivisibleArea(List<string> _telemetry)
    {
        _dto.RoomIsInsideASubdivisibleArea = RunSubworkflow<
                Area_GetInternalRoomsArgs, 
                Area_GetInternalRooms, 
                Area_GetInternalRoomsDto, 
                (bool isInsideAnyArea, ElementId? containingAreaId)
            >(
                new(
                      SubdivisibleAreas: _dto.SubdivisibleAreas
                    , Room: _dto.SelectedRoom
                    , GetInternalRoomsOptions: Area_GetInternalRoomsOptions.WholeRoom
                )
            );
    }

    public void InterruptWorkflowIfRoomIsNotInsideAnySubdivisibleArea(List<string> _telemetry)
    {
        if (_dto.RoomIsInsideASubdivisibleArea.isInsideAnyArea.Equals(false))
        {
            StopWorkflow($"The room is not inside any subdivisible area.", WorkflowInterruptionReason.Success);
        }
    }
    
    public void GetContainingSubdivisibleArea(List<string> _telemetry)
    {
        _dto.ContainingSubdivisibleArea = _dto.SubdivisibleAreas.First(a => a.Id.Equals(_dto.RoomIsInsideASubdivisibleArea.containingAreaId));
    }

    public void GetContainingSubdivisibleAreaBoundarySegments(List<string> _telemetry)
    {
        var options = new SpatialElementBoundaryOptions
        {
            SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish,
        };

        _dto.ContainingSubdivisibleAreaBoundarySegments = _dto.ContainingSubdivisibleArea.GetBoundarySegments(options).Select(a => a.ToList()).ToList();
    }

    public void GetContainingSubdivisibleAreaBottomFace(List<string> _telemetry)
    {
        _dto.ContainingSubdivisibleAreaBottomFace = RunSubworkflow<
              Face_Z0FromBoundarySegmentsArgs
            , Face_Z0FromBoundarySegments
            , Face_Z0FromBoundarySegmentsDto
            , Autodesk.Revit.DB.Face
            >
            (
                new(_dto.ContainingSubdivisibleAreaBoundarySegments)
            );
    }

    public void GenerateContainingAreaGridLines(List<string> _telemetry)
    {
        _dto.ContainingAreaGridLines = RunSubworkflow<
              Grid_LinesFromInternalOriginXYAlignedFaceArgs
            , Grid_LinesFromInternalOriginXYAlignedFace
            , Grid_LinesFromInternalOriginXYAlignedFaceDto
            , List<Line>
            >
            (
                new(_dto.ContainingSubdivisibleAreaBottomFace, 3, 3, false)
            );
    }

    public void GetRoomBoundarySegments(List<string> _telemetry)
    {
        var options = new SpatialElementBoundaryOptions
        {
            SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish,
        };

        _dto.RoomBoundarySegments = _dto.SelectedRoom.GetBoundarySegments(options).Select(a => a.ToList()).ToList();
    }

    public void GetRoomBottomFace(List<string> _telemetry)
    {
        _dto.RoomBottomFace = RunSubworkflow<
              Face_Z0FromBoundarySegmentsArgs
            , Face_Z0FromBoundarySegments
            , Face_Z0FromBoundarySegmentsDto
            , Autodesk.Revit.DB.Face
            >
            (
                new(_dto.RoomBoundarySegments)
            );
    }

    public void GenerateContainingAreaGridRemainingGridOutsideRoom(List<string> _telemetry)
    {
        _dto.ContainingAreaGridRemainingGridOutsideRoom = RunSubworkflow<
              Grid_LinesRemainingOutsideFaceArgs
            , Grid_LinesRemainingOutsideFace
            , Grid_LinesRemainingOutsideFaceDto
            , List<Line>
            >
            (
                new(_dto.ContainingAreaGridLines, _dto.RoomBottomFace, 3, 3, true)
            );
    }

    

    public void SetResult(List<string> _telemetry)
    {
        Result = true;
    }
}

public class DynamicFLoorPlanOptionGenerationDto : Dto
{
    [Print(nameof(TypeFormatter.ElementList))]
    public List<Element> Columns { get; set; }

    [Print(nameof(TypeFormatter.AreaList))]
    public List<Area> SubdivisibleAreas { get; set; }

    [Print(nameof(TypeFormatter.Room))]
    public Room SelectedRoom { get; set; }

    [Print(nameof(TypeFormatter.BooleanAndNullableElementIdTuple))]
    public (bool isInsideAnyArea, ElementId? containingAreaId) RoomIsInsideASubdivisibleArea { get; set; }

    [Print(nameof(TypeFormatter.Area))]
    public Area ContainingSubdivisibleArea { get; set; }


    public List<List<BoundarySegment>> ContainingSubdivisibleAreaBoundarySegments { get; set; }

    [Print(nameof(TypeFormatter.Face))]
    public Autodesk.Revit.DB.Face ContainingSubdivisibleAreaBottomFace { get; set; }

    [Print(nameof(TypeFormatter.LineList))]
    public List<Line> ContainingAreaGridLines { get; set; }

    [Print(nameof(TypeFormatter.BoundarySegmentListOfList))]
    public List<List<BoundarySegment>> RoomBoundarySegments { get; set; }

    [Print(nameof(TypeFormatter.Face))]
    public Autodesk.Revit.DB.Face RoomBottomFace { get; set; }


    [Print(nameof(TypeFormatter.LineList))]
    public List<Line> ContainingAreaGridRemainingGridOutsideRoom { get; set; }
}

public class SubdivisibleRoomsWithInsideColumns 
{
    public Room Room { get; set; }
    public List<Element> Columns { get; set; }
}