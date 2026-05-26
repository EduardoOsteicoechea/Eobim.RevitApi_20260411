using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Eobim.RevitApi.Framework;
using Eobim.RevitApi.MultiStepActions.Contour;
using Eobim.RevitApi.MultiStepActions.Face;

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

public class Area_GetInternalRooms : MultistepObservableAction<Area_GetInternalRoomsArgs, Area_GetInternalRoomsDto, (bool isInsideAnyArea, ElementId? containingAreaId)>
{
    public override void SafelyInitializeInputs(Area_GetInternalRoomsArgs args)
    {
        _dto.SubdivisibleAreas = args.SubdivisibleAreas;
        _dto.Room = args.Room;
        _dto.GetInternalRoomsOptions = args.GetInternalRoomsOptions;
    }

    protected override void SetActions()
    {
        Add(FilterLevelSharingAreas);
        Add(GetSameLevelAreasIdsWithBoundarySegments);
        Add(GetSameLevelAreasContours);
        Add(GetRoomBoundarySegments);
        Add(GetRoomContours);
        Add(GetRoomZ0Points);
        Add(ValidateIfAllRoomPointsAreInsideSolid);
        Add(SetResult);

    }

    public void FilterLevelSharingAreas(List<string> _telemetry)
    {
        var roomLevel = _dto.Room.LevelId;

        var result = _dto.SubdivisibleAreas.Where(a => a.LevelId == roomLevel).ToList();

        if (result is null) throw new ArgumentException("result is null");
        if (result.Count.Equals(0)) throw new ArgumentException("result.Count.Equals(0");

        _dto.AreasOnRoomLevel = result;
    }

    public void GetSameLevelAreasIdsWithBoundarySegments(List<string> _telemetry)
    {
        var result = new List<(ElementId areaId, List<List<BoundarySegment>>)>();

        var options = new SpatialElementBoundaryOptions
        {
            SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish,
        };

        foreach (var item in _dto.AreasOnRoomLevel)
        {
            var boundaries = item.GetBoundarySegments(options).Select(a => a.ToList()).ToList();

            result.Add((item.Id, boundaries));
        }

        if (result is null) throw new ArgumentException("result is null");
        if (result.Count.Equals(0)) throw new ArgumentException("result.Count.Equals(0");

        _dto.AreasIdsWithBoundarySegments = result;
    }

    public void GetSameLevelAreasContours(List<string> _telemetry)
    {
        var result = new List<(ElementId areaId, Autodesk.Revit.DB.Face bottomFace)>();

        for (int i = 0; i < _dto.AreasIdsWithBoundarySegments.Count; i++)
        {
            var item = _dto.AreasIdsWithBoundarySegments[i];

            var itemData = RunSubworkflow<
                  Face_Z0FromBoundarySegmentsArgs
                , Face_Z0FromBoundarySegments
                , Face_Z0FromBoundarySegmentsDto
                , Autodesk.Revit.DB.Face 
                >
                (
                    new(item.boundarySegments),
                    i
                );

            result.Add((item.areaId, itemData));
        }

        if (result is null) throw new ArgumentException("result is null");
        if (result.Count.Equals(0)) throw new ArgumentException("result.Count.Equals(0");

        _dto.AreasIdsWithZ0SolidsBottomFaces = result;
    }

    public void GetRoomBoundarySegments(List<string> _telemetry)
    {
        var options = new SpatialElementBoundaryOptions
        {
            SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish,
        };

        var result = _dto.Room.GetBoundarySegments(options).Select(a => a.ToList()).ToList();

        if (result is null) throw new ArgumentException("result is null");
        if (result.Count.Equals(0)) throw new ArgumentException("result.Count.Equals(0");

        _dto.RoomBoundarySegments = result;
    }

    public void GetRoomContours(List<string> _telemetry)
    {
        var result = RunSubworkflow<
              Contour_FromBoundarySegmentsArgs
            , Contour_FromBoundarySegments
            , Contour_FromBoundarySegmentsDto
            , List<CountourSegment>>
            (
                new(_dto.RoomBoundarySegments)
            );

        if (result is null) throw new ArgumentException("result is null");
        if (result.Count.Equals(0)) throw new ArgumentException("result.Count.Equals(0");

        _dto.RoomContours = result;
    }

    public void GetRoomZ0Points(List<string> _telemetry)
    {
        var result = new List<XYZ>();

        for (int i = 0; i < _dto.RoomContours.Count; i++)
        {
            var item = _dto.RoomContours[i];

            var points = item.Lines.SelectMany(line =>
            {
                var p1 = line.GetEndPoint(0);
                var p2 = line.GetEndPoint(1);
                var z0P1 = new XYZ(p1.X, p1.Y, 0);
                var z0P2 = new XYZ(p2.X, p2.Y, 0);
                return new List<XYZ> { z0P1, z0P2 };
            }).ToList();

            result.AddRange(points);
        }

        if (result is null) throw new ArgumentException("result is null");
        if (result.Count.Equals(0)) throw new ArgumentException("result.Count.Equals(0");

        _dto.RoomPoints = result;
    }
    public void ValidateIfAllRoomPointsAreInsideSolid(List<string> _telemetry)
    {
        var result = false;

        var roomPointsCount = _dto.RoomPoints.Count;

        for (int i = 0; i < _dto.AreasIdsWithZ0SolidsBottomFaces.Count; i++)
        {
            var face = _dto.AreasIdsWithZ0SolidsBottomFaces[i].face;

            bool allPointsInThisArea = true;

            for (int j = 0; j < roomPointsCount; j++)
            {
                var point = _dto.RoomPoints[j];

                if (!IsPointOnFace(point, face))
                {
                    allPointsInThisArea = false;
                    break;
                }
            }

            if (allPointsInThisArea)
            {
                result = true;
                break;
            }
        }

        _dto.RoomIsInsideValidation = (result, result ? _dto.SubdivisibleAreas.FirstOrDefault()?.Id : (ElementId?)null);
    }

    private bool IsPointOnFace(XYZ point, Autodesk.Revit.DB.Face face)
    {
        // 1. Attempt to project the 3D point onto the face
        IntersectionResult projection = face.Project(point);

        // If projection is null, the point does not align with the bounded area of the face at all
        if (projection == null) return false;

        // 2. Check if the point is actually touching the face.
        // If distance > 0, the point is hovering above/below the surface.
        double tolerance = 1e-6; // Standard Revit API tolerance to avoid floating point errors
        if (projection.Distance > tolerance) return false;

        // 3. Confirm the UV coordinates of the projection are inside the face boundaries
        return face.IsInside(projection.UVPoint);
    }

    public void SetResult(List<string> _telemetry)
    {
        Result = _dto.RoomIsInsideValidation;
    }
}

public class Area_GetInternalRoomsDto : Dto
{
    public List<Area> SubdivisibleAreas { get; set; }
    public Room Room { get; set; }
    public Area_GetInternalRoomsOptions GetInternalRoomsOptions { get; set; }
    public List<Area> AreasOnRoomLevel { get; set; }
    public List<(ElementId areaId, List<List<BoundarySegment>> boundarySegments)> AreasIdsWithBoundarySegments { get; set; }
    public List<(ElementId areaId, List<CountourSegment> countourSegments)> AreasIdsWithContourSegments { get; set; }
    public List<List<BoundarySegment>> RoomBoundarySegments { get; set; }
    public List<CountourSegment> RoomContours { get; set; }
    public List<(ElementId areaId, Solid solid)> AreasIdsWithZ0Solids { get; set; }
    public List<(ElementId areaId, Autodesk.Revit.DB.Face face)> AreasIdsWithZ0SolidsBottomFaces { get; set; }
    public List<XYZ> RoomPoints { get; set; }
    public (bool, ElementId?) RoomIsInsideValidation { get; set; }
}