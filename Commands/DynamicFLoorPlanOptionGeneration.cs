using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
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
        //Add(GetSubdivisibleRooms);
        Add(PromptUserToPickRoom);
        Add(ValidateSelectedRoomMatchWithSubdivisibleArea);
        //Add(GetSelectedRoomContourSegments);
        //Add(PlaceEnclosingModelLines, true, TransactionManagementOptions.RequiresDedicatedTransactionForAction);
        //Add(SetResult);
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

    //public void GetSubdivisibleRooms(List<string> _telemetry)
    //{
    //    var result = new FilteredElementCollector(_doc!)
    //        .WhereElementIsNotElementType()
    //        .OfCategory(BuiltInCategory.OST_Rooms)
    //        .Where(a => a.Name.Contains("subdivisible"))
    //        .Cast<Room>()
    //        .ToList();

    //    if (result is null)
    //    {
    //        throw new ArgumentException("No subdivisible rooms found.");
    //    }
    //    else
    //    {
    //        _telemetry.Add($"{result.Count} subdivisible rooms found.");

    //        _dto.SubdivisibleRooms = result;
    //    }
    //}

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

    //public void MatchSelectedSubdivisibleAreaWithInsideColumns(List<string> _telemetry)
    //{
    //    var result = FindColumnsInsideAreas(_dto.SelectedRoom, _dto.Columns, _dto.SubdivisibleAreas);

    //    if (result is null)
    //    {
    //        throw new ArgumentException($"Failed to match room to columns.");
    //    }
    //    else
    //    {
    //        _dto.SubdivisibleRoomWithInsideColumn = result;
    //    }
    //}

    public void ValidateSelectedRoomMatchWithSubdivisibleArea(List<string> _telemetry)
    {
        RunSubworkflow<
            Area_GetInternalRoomsArgs,
            Area_GetInternalRooms,
            Area_GetInternalRoomsDto,
            List<ElementId>>(
                new(
                      SubdivisibleAreas: _dto.SubdivisibleAreas
                    , Room: _dto.SelectedRoom
                    , GetInternalRoomsOptions: Area_GetInternalRoomsOptions.WholeRoom
                ));
    }

    private SubdivisibleRoomsWithInsideColumns FindColumnsInsideAreas(Room room, List<Element> columns, List<Area> areas)
    {
        var result = new SubdivisibleRoomsWithInsideColumns
        {
            Room = room,
            Columns = new List<Element>()
        };

        foreach (var column in columns)
        {
            if (room.IsPointInRoom(ColumnMassCenter(column)))
            {
                result.Columns.Add(column);
            }
        }

        return result;
    }

    private XYZ ColumnMassCenter(Element column)
    {
        // 1. Setup geometry extraction options
        Options geomOptions = new Options
        {
            ComputeReferences = false,
            DetailLevel = ViewDetailLevel.Fine
        };

        GeometryElement geomElement = column.get_Geometry(geomOptions);

        if (geomElement == null)
        {
            throw new ArgumentException("Cannot extract geometry from the provided column.");
        }

        XYZ centerOfMassSum = XYZ.Zero;
        double totalVolume = 0.0;

        // 2. Iterate through the geometry to find solids
        foreach (GeometryObject geomObj in geomElement)
        {
            if (geomObj is GeometryInstance geomInstance)
            {
                GeometryElement instanceGeom = geomInstance.GetInstanceGeometry();
                foreach (GeometryObject instObj in instanceGeom)
                {
                    if (instObj is Solid solid && solid.Volume > 0)
                    {
                        double volume = solid.Volume;
                        // Multiply centroid by volume to weight it properly
                        centerOfMassSum += solid.ComputeCentroid() * volume;
                        totalVolume += volume;
                    }
                }
            }
            else if (geomObj is Solid solid && solid.Volume > 0)
            {
                double volume = solid.Volume;
                centerOfMassSum += solid.ComputeCentroid() * volume;
                totalVolume += volume;
            }
        }

        // 3. Calculate the final weighted average for the center of mass
        if (totalVolume > 0)
        {
            return centerOfMassSum / totalVolume;
        }

        // 4. Fallback 1: Bounding Box center (if no physical solids with volume exist)
        BoundingBoxXYZ bbox = column.get_BoundingBox(null);

        if (bbox != null)
        {
            return (bbox.Min + bbox.Max) / 2.0;
        }

        // 5. Fallback 2: The original LocationPoint (insertion point)
        var columnLocation = column.Location as LocationPoint;

        if (columnLocation != null)
        {
            return columnLocation.Point;
        }

        throw new InvalidOperationException("Could not compute mass center: no valid geometry, bounding box, or location point found.");
    }

    public void GetSelectedRoomContourSegments(List<string> _telemetry)
    {
        var result = new List<CountourSegment>();

        var room = _dto.SelectedRoom;

        var options = new SpatialElementBoundaryOptions
        {
            SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish
        };

        var roomBoundarySegments = room.GetBoundarySegments(options) ?? throw new ArgumentNullException($"Failed to obtain roomBoundarySegments");

        var outerBoundary = roomBoundarySegments.FirstOrDefault() ?? throw new ArgumentNullException($"Failed to obtain first roomBoundarySegment");

        var outerBoundaryCurves = outerBoundary.Select(a => a.GetCurve()).ToList();

        for (int i = 0; i < outerBoundaryCurves.Count; i++)
        {
            var curve = outerBoundaryCurves[i];

            var contourSegment = new CountourSegment();

            var contourLines = new List<Line>();

            if (curve is Line) contourLines.Add(curve as Line);
            else 
            {
                var tesselation = curve.Tessellate().ToList();
                var lines = CreateLinesFromTesselation(tesselation);
                contourLines.AddRange(lines);
            }

            if (contourLines is null) throw new Exception("Failed to obtain contour lines. Result is null.");
            if (!contourLines.Any()) throw new Exception("Failed to obtain contour lines. Result is empty.");

            contourSegment.Lines = contourLines;
            result.Add(contourSegment);
        }

        if (result is null) throw new Exception("Failed to obtain contour lines. Result is null.");
        if (!result.Any()) throw new Exception("Failed to obtain contour lines. Result is empty.");

        _dto.SelectedRoomContourSegments = result;
    }

    private List<Line> CreateLinesFromTesselation(List<XYZ> curvePoints) 
    {
        var result = new List<Line>();

        for (int x = 0; x < curvePoints.Count - 1; x++)
        {
            var currentPoint = curvePoints[x];
            var nextPoint = curvePoints[x + 1];

            if (!currentPoint.IsAlmostEqualTo(nextPoint))
            {
                var line = Line.CreateBound(currentPoint, nextPoint);
                result.Add(line);
            }
        }

        return result;
    }

    public void PlaceEnclosingModelLines(List<string> _stateTrace)
    {
        if (_dto.SelectedRoomContourSegments == null || !_dto.SelectedRoomContourSegments.Any())
        {
            throw new InvalidOperationException("No contour segments available to calculate the bounding box.");
        }

        double minX = double.MaxValue;
        double minY = double.MaxValue;
        double maxX = double.MinValue;
        double maxY = double.MinValue;

        // Extract the Z elevation from the first available point to keep the lines coplanar
        double zHeight = _dto.SelectedRoomContourSegments
            .FirstOrDefault()?.Lines.FirstOrDefault()?.GetEndPoint(0).Z ?? 0.0;

        // 1. Calculate the bounding box limits dynamically from the contour segments
        foreach (var segment in _dto.SelectedRoomContourSegments)
        {
            foreach (var line in segment.Lines)
            {
                // Check both start and end points of each tessellated line
                for (int i = 0; i < 2; i++)
                {
                    XYZ pt = line.GetEndPoint(i);
                    minX = Math.Min(minX, pt.X);
                    minY = Math.Min(minY, pt.Y);
                    maxX = Math.Max(maxX, pt.X);
                    maxY = Math.Max(maxY, pt.Y);
                }
            }
        }
        
        // 2. Define the 4 corners of the enclosing orthogonal rectangle
        var p1 = new XYZ(minX, minY, zHeight);
        var p2 = new XYZ(maxX, minY, zHeight);
        var p3 = new XYZ(maxX, maxY, zHeight);
        var p4 = new XYZ(minX, maxY, zHeight);

        var lines = new List<Line>
        {
            Line.CreateBound(p1, p2),
            Line.CreateBound(p2, p3),
            Line.CreateBound(p3, p4),
            Line.CreateBound(p4, p1)
        };

        var plane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, p1);

        var sketchPlane = SketchPlane.Create(_doc, plane);

        foreach (var line in lines)
        {
            _doc!.Create.NewModelCurve(line, sketchPlane);
        }

        _stateTrace.Add($"Placed 4 enclosing model lines at Z elevation: {zHeight}");
    }

    public void SetResult(List<string> _telemetry)
    {
        Result = true;
    }
}

public class DynamicFLoorPlanOptionGenerationDto : Dto
{
    public List<Element> Columns { get; set; }
    //public List<Room> SubdivisibleRooms { get; set; }
    public List<Area> SubdivisibleAreas { get; set; }
    public Room SelectedRoom { get; set; }
    public SubdivisibleRoomsWithInsideColumns SubdivisibleRoomWithInsideColumn { get; set; }
    public List<CountourSegment> SelectedRoomContourSegments { get; set; }
}

public class SubdivisibleRoomsWithInsideColumns 
{
    public Room Room { get; set; }
    public List<Element> Columns { get; set; }
}

public class CountourSegment
{
    public bool? IsASingleLine => Lines.Count.Equals(1);
    public List<Line> Lines { get; set; }
}