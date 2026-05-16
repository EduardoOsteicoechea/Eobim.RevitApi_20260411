using Eobim.RevitApi.Framework;
using Autodesk.Revit.DB;
using Eobim.RevitApi.Core;

namespace Eobim.RevitApi.MultiStepActions;

public class DirectShape_ModelPlanarByBoundaryLines(Document doc, string workflowName)
    :
    MultistepObservableAction<DirectShape_ModelByBoundaryLinesDto, DirectShapeDMFAData>(doc, workflowName)
{
    public override void SafelyInitializeInputs(object[] args)
    {
        _dto.BoundaryLines = args[0] as List<Line>;
        _dto.ExtrusionDirection = args[1] as XYZ;
        _dto.ExtrusionThickness = (double)args[2];
        _dto.DirectShapeName = args[3] as string;
    }

    protected override void SetActions()
    {
        Add(GetCurveLoop);
        Add(GetEnclosingDimmensions);
        Add(GenerateSolid);
        Add(SetShape, false, TransactionManagementOptions.RequiresDedicatedTransactionForAction);
        Add(ExtractDirectShapeLeadFace);
        Add(SetResult);
    }

    public void GetCurveLoop(List<string> _stateTrace)
    {
        _dto.CurveLoop = new CurveLoop();
        foreach (var item in _dto.BoundaryLines)
        {
            _dto.CurveLoop.Append(item);
        }
    }

    public void GetEnclosingDimmensions(List<string> _stateTrace)
    {
        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;

        foreach (var item in _dto.BoundaryLines)
        {
            var p1 = item.GetEndPoint(0);
            var p2 = item.GetEndPoint(1);

            if (p1.X < minX) minX = p1.X;
            if (p2.X < minX) minX = p2.X;

            if (p1.X > maxX) maxX = p1.X;
            if (p2.X > maxX) maxX = p2.X;

            if (p1.Y < minY) minY = p1.Y;
            if (p2.Y < minY) minY = p2.Y;

            if (p1.Y > maxY) maxY = p1.Y;
            if (p2.Y > maxY) maxY = p2.Y;
        }

        _dto.MinX = minX;
        _dto.MinY = minY;
        _dto.MaxX = maxX;
        _dto.MaxY = maxY;
    }

    public void GenerateSolid(List<string> _stateTrace)
    {
        _dto.Solid = GeometryCreationUtilities.CreateExtrusionGeometry(
                new List<CurveLoop> { _dto.CurveLoop },
                _dto.ExtrusionDirection,
                _dto.ExtrusionThickness
            );
    }

    public void SetShape(List<string> _stateTrace)
    {
        var directShapeCategory = Category.GetCategory(_doc, BuiltInCategory.OST_GenericModel);

        _dto.DirectShape = DirectShape.CreateElement(_doc, directShapeCategory.Id);

        _dto.DirectShape.Name = _dto.DirectShapeName;

        _dto.DirectShape.SetShape(new List<GeometryObject> { _dto.Solid });
    }

    public void ExtractDirectShapeLeadFace(List<string> _stateTrace)
    {
        var faceGeometry = _dto.DirectShape.get_Geometry(new Options { ComputeReferences = true, DetailLevel = ViewDetailLevel.Fine });

        var solid = faceGeometry.OfType<Solid>().FirstOrDefault();

        var faces = solid?.Faces.Cast<Face>();

        var result = faces?.FirstOrDefault(a =>
        {
            var faceNormal = a.ComputeNormal(new UV(.5, .5));
            return faceNormal.IsAlmostEqualTo(_dto.ExtrusionDirection);
        });

        if (result is null) throw new NullReferenceException("Lead face not found.");

        _dto.DirectShapeLeadFace = result;
    }

    public void SetResult(List<string> _stateTrace)
    {
        Result = new DirectShapeDMFAData
        {
            BoundaryLines = _dto.BoundaryLines,
            ExtrusionDirection = _dto.ExtrusionDirection,
            ExtrusionThickness = _dto.ExtrusionThickness,
            DirectShape = _dto.DirectShape
        };
    }
}
public class DirectShape_ModelByBoundaryLinesDto : Dto
{
    public List<Line> BoundaryLines { get; set; }
    public XYZ ExtrusionDirection { get; set; }
    public double ExtrusionThickness { get; set; }
    public string DirectShapeName { get; set; }

    public double MinX { get; set; }
    public double MinY { get; set; }
    public double MaxX { get; set; }
    public double MaxY { get; set; }

    public CurveLoop CurveLoop { get; set; }
    public Solid Solid { get; set; }
    public DirectShape DirectShape { get; set; }

    public Face DirectShapeLeadFace { get; set; }
}