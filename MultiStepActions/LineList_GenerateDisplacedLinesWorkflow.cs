using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Eobim.RevitApi.Framework;
namespace Eobim.RevitApi.Workflows;

public class LineList_GenerateDisplacedLinesWorkflow(Document doc, string parentCommandName)
:
MultistepObservableAction<LineList_GenerateDisplacedLinesWorkflowDto, List<Line>>(doc, parentCommandName)
{
    public override void SafelyInitializeInputs(object[] args)
    {
        _dto.InputLineList = args[0] as List<Line>;
        _dto.DisplacementThickness = (double)args[1];
        _dto.DisplacementValue = _dto.DisplacementThickness * .5;
    }

    protected override void SetActions()
    {
        Add(GenerateLines, mustLogAction: true, TransactionManagementOptions.TransactionlessAction);
        Add(SetDisplacementValue, mustLogAction: true, TransactionManagementOptions.TransactionlessAction);
        Add(SetResult, mustLogAction: true, TransactionManagementOptions.TransactionlessAction);
    }

    private void SetDisplacementValue(List<string> telemetry)
    {
        _dto.DisplacementValue = _dto.DisplacementThickness * .5;
    }

    private void GenerateLines(List<string> telemetry)
    {
        var result = new List<Line>();

        for (int i = 0; i < _dto.InputLineList.Count; i++)
        {
            var line = _dto.InputLineList[i];
            XYZ p0 = line.GetEndPoint(0);
            XYZ p1 = line.GetEndPoint(1);

            XYZ direction = (p1 - p0).Normalize();
            //XYZ direction = (p1 - p0).Normalize().Negate();

            // Displace both the START and END points
            XYZ newP0 = p0 + (direction * _dto.DisplacementValue);
            XYZ newP1 = p1 + (direction * _dto.DisplacementValue);

            // Safety check: Ensure the line hasn't been shrunk out of existence
            //if (newP0.DistanceTo(newP1) > 0.004)
            // Discard small lines that get covered by the displacement of previous and following lines.
            if (newP0.DistanceTo(newP1) > _dto.DisplacementValue)
            {
                result.Add(Line.CreateBound(newP0, newP1));
            }
            //else
            //{
            //    result.Add(line);
            //}
        }

        _dto.OutputDisplacedLines = result;
    }

    private void SetResult(List<string> telemetry)
    {
        if (!_dto.OutputDisplacedLines.Any()) throw new InvalidOperationException("No lines were generated.");

        Result = _dto.OutputDisplacedLines;
    }
}

public class LineList_GenerateDisplacedLinesWorkflowDto : IDto
{
    public List<Line> InputLineList { get; set; }
    public double DisplacementThickness { get; set; }
    public double DisplacementValue { get; set; }
    public List<Line> OutputDisplacedLines { get; set; }

    public List<(string, object)> ToObservableObject()
    {
        return new List<(string, object)>
        {
            ("InputLineList", InputLineList != null),
            ("Thickness", DisplacementThickness),
            ("GeneratedLinesCount", OutputDisplacedLines?.Count ?? 0)
        };

    }
}