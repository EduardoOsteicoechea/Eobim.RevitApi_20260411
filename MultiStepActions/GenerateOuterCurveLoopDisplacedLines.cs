using Autodesk.Revit.DB;
using Eobim.RevitApi.Framework;
namespace Eobim.RevitApi.Workflows;

public class CurveLoop_GenerateDisplacedLinesWorkflow(Document doc, string parentCommandName)
: 
MultistepObservableAction<GenerateOuterCurveLoopDisplacedLinesDto, List<Line>>(doc, parentCommandName)
{
    public void InitializeInputs(CurveLoop curveLoop, double thickness)
    {
        _dto.InputCurveLoop = curveLoop;
        _dto.DisplacementThickness = thickness;
    }

    protected override void SetActions()
    {
        Add(GenerateLines, mustLogAction: true, TransactionManagementOptions.TransactionlessAction);
    }

    private void GenerateLines(List<string> telemetry)
    {
        if (_dto.InputCurveLoop == null) throw new ArgumentNullException(nameof(_dto.InputCurveLoop));

        var result = new List<Line>();
        var curveList = _dto.InputCurveLoop.ToList();

        for (int i = 0; i < curveList.Count; i++)
        {
            Curve curve = curveList[i];

            if (curve is Line line)
            {
                XYZ p0 = line.GetEndPoint(0);
                XYZ p1 = line.GetEndPoint(1);

                XYZ direction = (p1 - p0).Normalize().Negate();

                // Displace the START point forward by the cardboard thickness
                XYZ newP0 = p0 + (direction * (_dto.DisplacementThickness * 0.5));
                XYZ newP1 = p1 + (direction * (_dto.DisplacementThickness * 0.5));



                // Safety check: Ensure the line hasn't been shrunk out of existence
                if (newP0.DistanceTo(newP1) > 0.004)
                {
                    result.Add(Line.CreateBound(newP0, newP1));
                }
                else
                {
                    result.Add(line);
                }
            }
        }

        if (!result.Any()) throw new InvalidOperationException("No lines were generated.");

        _dto.OutputDisplacedLines = result;

        Result = result;
    }
}

public class GenerateOuterCurveLoopDisplacedLinesDto : IDto
{
    public CurveLoop? InputCurveLoop { get; set; }
    public double DisplacementThickness { get; set; }
    public List<Line>? OutputDisplacedLines { get; set; }

    public List<(string, object)> ToObservableObject()
    {
        return new List<(string, object)>
        {
            ("CurveLoopProvided", InputCurveLoop != null),
            ("Thickness", DisplacementThickness),
            ("GeneratedLinesCount", OutputDisplacedLines?.Count ?? 0)
        };

    }
}