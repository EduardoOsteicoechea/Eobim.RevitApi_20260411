using Autodesk.Revit.DB;
using Eobim.RevitApi.DFMA;
using Eobim.RevitApi.Framework;

namespace Eobim.RevitApi.DFMA;

public record RevitDFMA_ExtractFloorDataArgs(Floor InterestFloor);

internal class RevitDFMA_ExtractFloorData : MultistepObservableAction<RevitDFMA_ExtractFloorDataArgs, RevitDFMA_ExtractFloorDataDto, FloorDFMAData>
{
    public override void SafelyInitializeInputs(RevitDFMA_ExtractFloorDataArgs args)
    {
        _dto.InterestFloor = args.InterestFloor;
    }

    protected override void SetActions()
    {
        /* 1 */
        Add(GetTopFace);
        /* 2 */
        Add(GetBottomFace);
        /* 3 */
        Add(GetTopFaceHighestPoint);
        /* 4 */
        Add(GetBottomFaceLowestPoint);
        /* 5 */
        Add(GetThickness);
        /* 6 */
        Add(GetBottomFaceOuterCurveLoop);
        /* 7 */
        Add(GetTopFaceOuterCurveLoop);
        /* 8 */
        Add(SetResult);
    }

    public void GetTopFace(List<string> _telemetry)
    {
        IList<Reference> references = HostObjectUtils.GetTopFaces(_dto.InterestFloor as HostObject);

        if (references.Count == 0)
        {
            throw new Exception($"No top face found for element with id: {_dto.InterestFloor.Id}");
        }

        var result = _dto.InterestFloor.GetGeometryObjectFromReference(references[0]) as Face;

        if (result is null) throw new NullReferenceException();

        _dto.InterestFloorTopFace = result;
    }

    public void GetBottomFace(List<string> _telemetry)
    {
        IList<Reference> references = HostObjectUtils.GetBottomFaces(_dto.InterestFloor as HostObject);

        if (references.Count == 0)
        {
            throw new Exception($"No top face found for element with id: {_dto.InterestFloor.Id}");
        }

        var result = _dto.InterestFloor.GetGeometryObjectFromReference(references[0]) as Face;

        if (result is null) throw new NullReferenceException();

        _dto.InterestFloorBottomFace = result;
    }

    public void GetTopFaceHighestPoint(List<string> _telemetry)
    {
        var result = this.GetHighestPointOnFace(_dto.InterestFloorTopFace);

        if (result is null) throw new NullReferenceException();

        _dto.InterestFloorTopFaceHighestPoint = result;
    }

    public void GetBottomFaceLowestPoint(List<string> _telemetry)
    {
        var result = this.GetLowestPointOnFace(_dto.InterestFloorBottomFace);

        if (result is null) throw new NullReferenceException();

        _dto.InterestFloorBottomFaceLowestPoint = result;
    }

    public XYZ GetHighestPointOnFace(Face face)
    {
        var curveLoops = face.GetEdgesAsCurveLoops();

        var curves = curveLoops.SelectMany(a => a).ToList();

        var points = curves.Select(a => a.GetEndPoint(0)).Concat(curves.Select(a => a.GetEndPoint(1))).ToList();

        return points.OrderByDescending(a => a.Z).First();
    }

    public XYZ GetLowestPointOnFace(Face face)
    {
        var curveLoops = face.GetEdgesAsCurveLoops();

        var curves = curveLoops.SelectMany(a => a).ToList();

        var points = curves.Select(a => a.GetEndPoint(0)).Concat(curves.Select(a => a.GetEndPoint(1))).ToList();

        return points.OrderBy(a => a.Z).First();
    }

    public void GetThickness(List<string> _telemetry)
    {
        var result = _dto.InterestFloorTopFaceHighestPoint.Z - _dto.InterestFloorBottomFaceLowestPoint.Z;

        if (result.Equals(0)) throw new InvalidOperationException("The calculated common height is zero, which may indicate an issue with the input data.");

        _dto.FamilyInstancesCommonHeight = result;
    }

    public void GetBottomFaceOuterCurveLoop(List<string> _telemetry)
    {
        CurveLoop result = GetFaceOuterCurveLoop(_telemetry, _dto.InterestFloorBottomFace);

        if (result is null) throw new NullReferenceException();

        _dto.BottomFaceOuterCurveLoop = result;
    }

    public void GetTopFaceOuterCurveLoop(List<string> _telemetry)
    {
        CurveLoop result = GetFaceOuterCurveLoop(_telemetry, _dto.InterestFloorTopFace);

        if (result is null) throw new NullReferenceException();

        _dto.TopFaceOuterCurveLoop = result;
    }

    public CurveLoop GetFaceOuterCurveLoop(List<string> _telemetry, Face face)
    {
        CurveLoop result = null;

        IList<CurveLoop> loops = face.GetEdgesAsCurveLoops();

        if (loops.Count.Equals(1))
        {
            result = loops[0];
        }
        else if (face is PlanarFace planarFace)
        {
            XYZ normal = planarFace.FaceNormal;

            foreach (CurveLoop loop in loops)
            {
                if (loop.IsCounterclockwise(normal))
                {
                    result = loop;
                    break;
                }
            }
        }
        else
        {
            BoundingBoxUV bbox = face.GetBoundingBox();
            UV center = new UV((bbox.Min.U + bbox.Max.U) / 2, (bbox.Min.V + bbox.Max.V) / 2);
            XYZ normal = face.ComputeNormal(center);

            foreach (CurveLoop loop in loops)
            {
                if (loop.IsCounterclockwise(normal))
                {
                    result = loop;
                    break;
                }
            }
        }

        if (result is null) throw new NullReferenceException();

        return result;
    }

    public void SetResult(List<string> _telemetry)
    {
        var area = _dto.InterestFloor.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED)?.AsDouble() ?? 0.0;
        var volume = _dto.InterestFloor.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED)?.AsDouble() ?? 0.0;
        
        Result = new FloorDFMAData
        {
            Id = _dto.InterestFloor.Id,
            Name = _dto.InterestFloor.Name,
            Area = area,
            Volume = volume,
            TopFace = _dto.InterestFloorTopFace,
            BottomFace = _dto.InterestFloorBottomFace,
            TopFaceHighestPoint = _dto.InterestFloorTopFaceHighestPoint,
            BottomFaceLowestPoint = _dto.InterestFloorBottomFaceLowestPoint,
            Thickness = _dto.FamilyInstancesCommonHeight,
            BottomFaceOuterCurveLoop = _dto.BottomFaceOuterCurveLoop,
            TopFaceOuterCurveLoop = _dto.TopFaceOuterCurveLoop
        };
    }
}

public class RevitDFMA_ExtractFloorDataDto : Dto
{
    [Print(nameof(TypeFormatter.Floor))]
    public Floor InterestFloor { get; set; }

    [Print(nameof(TypeFormatter.Face))]
    public Face InterestFloorTopFace { get; set; }

    [Print(nameof(TypeFormatter.Face))]
    public Face InterestFloorBottomFace { get; set; }

    [Print(nameof(TypeFormatter.XYZ))]
    public XYZ InterestFloorTopFaceHighestPoint { get; set; }

    [Print(nameof(TypeFormatter.XYZ))]
    public XYZ InterestFloorBottomFaceLowestPoint { get; set; }

    [Print(nameof(TypeFormatter.Double))]
    public double FamilyInstancesCommonHeight { get; set; }

    [Print(nameof(TypeFormatter.CurveLoop))]
    public CurveLoop BottomFaceOuterCurveLoop { get; set; }

    [Print(nameof(TypeFormatter.CurveLoop))]
    public CurveLoop TopFaceOuterCurveLoop { get; set; }
}