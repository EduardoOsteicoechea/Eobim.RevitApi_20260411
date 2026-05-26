using Autodesk.Revit.DB;
using Eobim.RevitApi.Framework;
using Eobim.RevitApi.MultiStepActions.Contour;

namespace Eobim.RevitApi.MultiStepActions.Face;

public record Face_Z0FromBoundarySegmentsArgs
(
      List<List<BoundarySegment>> Segments
);

public class Face_Z0FromBoundarySegments : MultistepObservableAction<Face_Z0FromBoundarySegmentsArgs, Face_Z0FromBoundarySegmentsDto, Autodesk.Revit.DB.Face>
{
    public override void SafelyInitializeInputs(Face_Z0FromBoundarySegmentsArgs args)
    {
        _dto.Segments = args.Segments;
    }
    protected override void SetActions()
    {
        Add(GetContours);
        Add(GenerateZ0Solid);
        Add(GetZ0SolidsBottomFace);
        Add(SetResult);
    }

    public void GetContours(List<string> _telemetry)
    {
        var areaBoundarySegments = _dto.Segments;

        var result = RunSubworkflow<
                Contour_FromBoundarySegmentsArgs
                , Contour_FromBoundarySegments
                , Contour_FromBoundarySegmentsDto
                , List<CountourSegment>>
                (
                    new(_dto.Segments)
                );

        if (result is null) throw new ArgumentException("result is null");
        if (result.Count.Equals(0)) throw new ArgumentException("result.Count.Equals(0");

        _dto.ContourSegments = result;
    }

    public void GenerateZ0Solid(List<string> _telemetry)
    {
        var curveLoop = new CurveLoop();

        foreach (var contourSegment in _dto.ContourSegments)
        {
            foreach (var line in contourSegment.Lines)
            {
                var p1 = line.GetEndPoint(0);
                var p2 = line.GetEndPoint(1);
                var z0P1 = new XYZ(p1.X, p1.Y, 0);
                var z0P2 = new XYZ(p2.X, p2.Y, 0);
                curveLoop.Append(Line.CreateBound(z0P1, z0P2));
            }
        }

        var result = GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { curveLoop }, XYZ.BasisZ, 10);

        if (result is null) throw new ArgumentException("result is null");

        _dto.Solid = result;
    }

    public void GetZ0SolidsBottomFace(List<string> _telemetry)
    {
        var result = _dto.Solid
            .Faces
            .Cast<Autodesk.Revit.DB.Face>()
            .ToList()
            .First(a =>
                a.ComputeNormal(new UV(.5, .5))
                .IsAlmostEqualTo(XYZ.BasisZ.Negate())
            );

        if (result is null) throw new ArgumentException($"result is null");

        _dto.Face = result;
    }

    public void SetResult(List<string> _telemetry)
    {
        Result = _dto.Face;
    }
}

public class Face_Z0FromBoundarySegmentsDto : Dto
{
    public List<List<BoundarySegment>> Segments { get; set; }
    public List<CountourSegment> ContourSegments { get; set; }
    public Solid Solid { get; set; }
    public Autodesk.Revit.DB.Face Face { get; set; }
}