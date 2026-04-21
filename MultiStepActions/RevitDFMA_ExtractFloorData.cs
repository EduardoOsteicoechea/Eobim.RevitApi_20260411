using Autodesk.Revit.DB;
using Eobim.RevitApi.Core;
using Eobim.RevitApi.Framework;

namespace Eobim.RevitApi.MultiStepActions;

internal class RevitDFMA_ExtractFloorData(Document doc, string workflowName)
    :
    MultistepObservableAction<RevitDFMA_ExtractFloorDataDto, FloorDFMAData>(doc, workflowName)
{
    public override void SafelyInitializeInputs(object[] args)
    {
        _dto.InterestFloor = args[0] as Floor;
    }

    protected override void SetActions()
    {
        /* 1 */
        Add(GetInterestFloorTopFace);
        /* 2 */
        Add(GetInterestFloorBottomFace);
        /* 3 */
        Add(GetInterestFloorTopFaceHighestPoint);
        /* 4 */
        Add(GetInterestFloorBottomFaceLowestPoint);
        /* 5 */
        Add(GetFamilyInstancesCommonHeight);
        /* 6 */
        Add(GetInterestFloorOuterCurveLoop);
        /* 7 */
        Add(SetResult);
    }
    public void GetInterestFloorTopFace(List<string> _telemetry)
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

    public void GetInterestFloorBottomFace(List<string> _telemetry)
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

    public void GetInterestFloorTopFaceHighestPoint(List<string> _telemetry)
    {
        var result = this.GetHighestPointOnFace(_dto.InterestFloorTopFace);

        if (result is null) throw new NullReferenceException();

        _dto.InterestFloorTopFaceHighestPoint = result;
    }

    public void GetInterestFloorBottomFaceLowestPoint(List<string> _telemetry)
    {
        var result = this.GetHighestPointOnFace(_dto.InterestFloorBottomFace);

        if (result is null) throw new NullReferenceException();

        _dto.InterestFloorBottomFaceLowestPoint = result;
    }

    public XYZ GetHighestPointOnFace(Face face)
    {
        var curveLoops = face.GetEdgesAsCurveLoops();

        var curves = curveLoops.SelectMany(a => a).ToList();

        var points = curves.Select(a => a.GetEndPoint(0)).Concat(curves.Select(a => a.GetEndPoint(1))).ToList();

        return points.OrderBy(a => a.Z).First();
    }

    public void GetFamilyInstancesCommonHeight(List<string> _telemetry)
    {
        var result = _dto.InterestFloorTopFaceHighestPoint.Z - _dto.InterestFloorBottomFaceLowestPoint.Z;

        if (result.Equals(0)) throw new InvalidOperationException("The calculated common height is zero, which may indicate an issue with the input data.");

        _dto.FamilyInstancesCommonHeight = result;
    }

    public void GetInterestFloorOuterCurveLoop(List<string> _telemetry)
    {
        CurveLoop result = null;

        var face = _dto.InterestFloorBottomFace;

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

        _dto.OuterCurveLoop = result;
    }

    public void SetResult(List<string> _telemetry)
    {
        Result = new FloorDFMAData
        {
            Id = _dto.InterestFloor.Id,
            Name = _dto.InterestFloor.Name,
            Area = _dto.InterestFloor.GetParameters("Area").FirstOrDefault()?.AsDouble() ?? 0,
            Volume = _dto.InterestFloor.GetParameters("Volume").FirstOrDefault()?.AsDouble() ?? 0,
            TopFace = _dto.InterestFloorTopFace,
            BottomFace = _dto.InterestFloorBottomFace,
            TopFaceHighestPoint = _dto.InterestFloorTopFaceHighestPoint,
            BottomFaceLowestPoint = _dto.InterestFloorBottomFaceLowestPoint,
            Thickness = _dto.FamilyInstancesCommonHeight,
            OuterCurveLoop = _dto.OuterCurveLoop
        };
    }
}

public class FloorDFMAData
{
    public ElementId Id { get; set; }
    public string Name { get; set; }
    public double Area { get; set; }
    public double Volume { get; set; }
    public Face TopFace { get; set; }
    public Face BottomFace { get; set; }
    public XYZ TopFaceHighestPoint { get; set; }
    public XYZ BottomFaceLowestPoint { get; set; }
    public double Thickness { get; set; }
    public CurveLoop OuterCurveLoop { get; set; }
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
    public CurveLoop OuterCurveLoop { get; set; }
}