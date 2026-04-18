using Autodesk.Revit.DB;
using Eobim.RevitApi.Framework;

namespace Eobim.RevitApi.Workflows;

public class GenerateOuterCurveLoopDisplacedLinesDto : IDto
{
    // Inputs
    public CurveLoop? InputCurveLoop { get; set; }
    public double DisplacementThickness { get; set; }

    // Output
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

public class GenerateOuterCurveLoopDisplacedLinesWorkflow(Document doc, string parentCommandName)
    : MultistepObservableAction<GenerateOuterCurveLoopDisplacedLinesDto, List<Line>>(doc, parentCommandName)
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

                XYZ direction = (p1 - p0).Normalize();

                // Displace the START point forward by the cardboard thickness
                XYZ newP0 = p0 + direction * _dto.DisplacementThickness;

                // Safety check: Ensure the line hasn't been shrunk out of existence
                if (newP0.DistanceTo(p1) > 0.004)
                {
                    result.Add(Line.CreateBound(newP0, p1));
                }
                //else
                //{
                //    result.Add(line);
                //}
            }
        }

        if (!result.Any()) throw new InvalidOperationException("No lines were generated.");

        _dto.OutputDisplacedLines = result;

        // CRITICAL: Set the Result property so the parent command can retrieve it!
        Result = result;

        telemetry.Add($"Successfully generated {result.Count} displaced lines.");
    }
}