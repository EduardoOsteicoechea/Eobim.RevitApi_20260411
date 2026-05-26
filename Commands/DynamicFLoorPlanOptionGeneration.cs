using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Eobim.RevitApi.Framework;
using Eobim.RevitApi.MultiStepActions;
using Eobim.RevitApi.SelectionFilter;

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
        _dto.RoomIsInsideASubdivisibleArea = RunSubworkflow<Area_GetInternalRoomsArgs, Area_GetInternalRooms, Area_GetInternalRoomsDto, (bool isInsideAnyArea, ElementId? containingAreaId)>(new(
              SubdivisibleAreas: _dto.SubdivisibleAreas
            , Room: _dto.SelectedRoom
            , GetInternalRoomsOptions: Area_GetInternalRoomsOptions.WholeRoom
        ));
    }

    public void InterruptWorkflowIfRoomIsNotInsideAnySubdivisibleArea(List<string> _telemetry)
    {
        if (_dto.RoomIsInsideASubdivisibleArea.isInsideAnyArea.Equals(false))
        {
            StopWorkflow($"The room is not inside any subdivisible area.", WorkflowInterruptionReason.Success);
        }
    }

    //private SubdivisibleRoomsWithInsideColumns FindColumnsInsideAreas(Room room, List<Element> columns, List<Area> areas)
    //{
    //    var result = new SubdivisibleRoomsWithInsideColumns
    //    {
    //        Room = room,
    //        Columns = new List<Element>()
    //    };

    //    foreach (var column in columns)
    //    {
    //        if (room.IsPointInRoom(ColumnMassCenter(column)))
    //        {
    //            result.Columns.Add(column);
    //        }
    //    }

    //    return result;
    //}

    //private XYZ ColumnMassCenter(Element column)
    //{
    //    // 1. Setup geometry extraction options
    //    Options geomOptions = new Options
    //    {
    //        ComputeReferences = false,
    //        DetailLevel = ViewDetailLevel.Fine
    //    };

    //    GeometryElement geomElement = column.get_Geometry(geomOptions);

    //    if (geomElement == null)
    //    {
    //        throw new ArgumentException("Cannot extract geometry from the provided column.");
    //    }

    //    XYZ centerOfMassSum = XYZ.Zero;
    //    double totalVolume = 0.0;

    //    // 2. Iterate through the geometry to find solids
    //    foreach (GeometryObject geomObj in geomElement)
    //    {
    //        if (geomObj is GeometryInstance geomInstance)
    //        {
    //            GeometryElement instanceGeom = geomInstance.GetInstanceGeometry();
    //            foreach (GeometryObject instObj in instanceGeom)
    //            {
    //                if (instObj is Solid solid && solid.Volume > 0)
    //                {
    //                    double volume = solid.Volume;
    //                    // Multiply centroid by volume to weight it properly
    //                    centerOfMassSum += solid.ComputeCentroid() * volume;
    //                    totalVolume += volume;
    //                }
    //            }
    //        }
    //        else if (geomObj is Solid solid && solid.Volume > 0)
    //        {
    //            double volume = solid.Volume;
    //            centerOfMassSum += solid.ComputeCentroid() * volume;
    //            totalVolume += volume;
    //        }
    //    }

    //    // 3. Calculate the final weighted average for the center of mass
    //    if (totalVolume > 0)
    //    {
    //        return centerOfMassSum / totalVolume;
    //    }

    //    // 4. Fallback 1: Bounding Box center (if no physical solids with volume exist)
    //    BoundingBoxXYZ bbox = column.get_BoundingBox(null);

    //    if (bbox != null)
    //    {
    //        return (bbox.Min + bbox.Max) / 2.0;
    //    }

    //    // 5. Fallback 2: The original LocationPoint (insertion point)
    //    var columnLocation = column.Location as LocationPoint;

    //    if (columnLocation != null)
    //    {
    //        return columnLocation.Point;
    //    }

    //    throw new InvalidOperationException("Could not compute mass center: no valid geometry, bounding box, or location point found.");
    //}

    

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
    public SubdivisibleRoomsWithInsideColumns SubdivisibleRoomWithInsideColumn { get; set; }
}

public class SubdivisibleRoomsWithInsideColumns 
{
    public Room Room { get; set; }
    public List<Element> Columns { get; set; }
}