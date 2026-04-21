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
        Add(GenerateSolid);
        Add(SetShape, false, TransactionManagementOptions.RequiresDedicatedTransactionForAction);
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

    public CurveLoop CurveLoop { get; set; }
    public Solid Solid { get; set; }
    public DirectShape DirectShape{ get; set; }
}

public class DirectShapeDMFAData
{
    public List<Line> BoundaryLines { get; set; }
    public XYZ ExtrusionDirection { get; set; }
    public double ExtrusionThickness { get; set; }
    public DirectShape DirectShape { get; set; }
    public Line PrintFaceLine { get; set; }
    public Reference PrintFaceReference { get; set; }
    public Face PrintFace { get; set; }
}
