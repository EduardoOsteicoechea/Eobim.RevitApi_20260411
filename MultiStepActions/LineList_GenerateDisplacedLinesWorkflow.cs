//using Autodesk.Revit.DB;
//using Eobim.RevitApi.Framework;

//namespace Eobim.RevitApi.MultiStepActions;

//public record LineList_GenerateDisplacedLinesWorkflowArgs(List<Line> InputLineList, double DisplacementThickness, double DisplacementValue);

//public class LineList_GenerateDisplacedLinesWorkflow(Document doc, string parentCommandName)
//:
//MultistepObservableAction<LineList_GenerateDisplacedLinesWorkflowArgs, LineList_GenerateDisplacedLinesWorkflowDto, List<Line>>(doc, parentCommandName)
//{
//    public override void SafelyInitializeInputs(LineList_GenerateDisplacedLinesWorkflowArgs args)
//    {
//        _dto.InputLineList = args.InputLineList;
//        _dto.DisplacementThickness = args.DisplacementThickness;
//        _dto.DisplacementValue = args.DisplacementValue;
//    }

//    protected override void SetActions()
//    {
//        Add(GenerateLines, mustLogAction: true, TransactionManagementOptions.TransactionlessAction);
//        Add(SetDisplacementValue, mustLogAction: true, TransactionManagementOptions.TransactionlessAction);
//        Add(SetResult, mustLogAction: true, TransactionManagementOptions.TransactionlessAction);
//    }

//    private void SetDisplacementValue(List<string> telemetry)
//    {
//        _dto.DisplacementValue = _dto.DisplacementThickness * .5;
//    }

//    private void GenerateLines(List<string> telemetry)
//    {
//        var result = new List<Line>();

//        for (int i = 0; i < _dto.InputLineList.Count; i++)
//        {
//            var line = _dto.InputLineList[i];
//            XYZ p0 = line.GetEndPoint(0);
//            XYZ p1 = line.GetEndPoint(1);

//            XYZ direction = (p1 - p0).Normalize();

//            // Displace the whole line in the line direction.
//            XYZ newP0 = p0 + (direction * _dto.DisplacementValue);
//            XYZ newP1 = p1 + (direction * _dto.DisplacementValue);

//            // Discard small lines that get covered by the displacement of previous and following lines.
//            if (newP0.DistanceTo(newP1) > _dto.DisplacementValue)
//            {
//                result.Add(Line.CreateBound(newP0, newP1));
//            }
//        }

//        _dto.OutputDisplacedLines = result;
//    }

//    private void SetResult(List<string> telemetry)
//    {
//        if (!_dto.OutputDisplacedLines.Any()) throw new InvalidOperationException("No lines were generated.");

//        Result = _dto.OutputDisplacedLines;
//    }
//}

//public class LineList_GenerateDisplacedLinesWorkflowDto : IDto
//{
//    public List<Line> InputLineList { get; set; }
//    public double DisplacementThickness { get; set; }
//    public double DisplacementValue { get; set; }
//    public List<Line> OutputDisplacedLines { get; set; }

//    public List<(string, object)> ToObservableObject()
//    {
//        return new List<(string, object)>
//        {
//            ("InputLineList", InputLineList != null),
//            ("Thickness", DisplacementThickness),
//            ("GeneratedLinesCount", OutputDisplacedLines?.Count ?? 0)
//        };

//    }
//}

using Autodesk.Revit.DB;
using Eobim.RevitApi.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Eobim.RevitApi.MultiStepActions;

public record LineList_GenerateDisplacedLinesWorkflowArgs(List<Line> InputLineList, double DisplacementThickness, double DisplacementValue);

public class LineList_GenerateDisplacedLinesWorkflow(Document doc, string parentCommandName)
: MultistepObservableAction<LineList_GenerateDisplacedLinesWorkflowArgs, LineList_GenerateDisplacedLinesWorkflowDto, List<Line>>(doc, parentCommandName)
{
    public override void SafelyInitializeInputs(LineList_GenerateDisplacedLinesWorkflowArgs args)
    {
        _dto.InputLineList = args.InputLineList;
        _dto.DisplacementThickness = args.DisplacementThickness;
        _dto.DisplacementValue = args.DisplacementValue;
    }

    protected override void SetActions()
    {
        // 1. Calculate the math FIRST
        Add(SetDisplacementValue, mustLogAction: true, TransactionManagementOptions.TransactionlessAction);

        // 2. Generate the lines SECOND
        Add(GenerateLines, mustLogAction: true, TransactionManagementOptions.TransactionlessAction);

        // 3. Return the result
        Add(SetResult, mustLogAction: true, TransactionManagementOptions.TransactionlessAction);
    }

    private void SetDisplacementValue(List<string> telemetry)
    {
        _dto.DisplacementValue = _dto.DisplacementThickness * 0.5;
        telemetry.Add($"Calculated Displacement Value: {_dto.DisplacementValue}");
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
            double originalLength = p0.DistanceTo(p1);

            // Discard small lines that would invert or collapse if we trimmed them from both sides
            if (originalLength <= (_dto.DisplacementValue * 2))
            {
                telemetry.Add($"Skipped line {i}: Too short to be displaced.");
                continue;
            }

            // Shrink the line: P0 moves forward, P1 moves backward
            XYZ newP0 = p0 + (direction * _dto.DisplacementValue);
            XYZ newP1 = p1 + (direction * _dto.DisplacementValue); // Notice the minus sign here!

            result.Add(Line.CreateBound(newP0, newP1));
        }

        _dto.OutputDisplacedLines = result;
    }

    private void SetResult(List<string> telemetry)
    {
        if (_dto.OutputDisplacedLines == null || !_dto.OutputDisplacedLines.Any())
            throw new InvalidOperationException("No lines were generated.");

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
            ("DisplacementValue", DisplacementValue),
            ("GeneratedLinesCount", OutputDisplacedLines?.Count ?? 0)
        };
    }
}