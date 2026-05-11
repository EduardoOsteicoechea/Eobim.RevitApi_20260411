using Autodesk.Revit.DB;
using Eobim.RevitApi.Commands;
using Eobim.RevitApi.Framework;

namespace Eobim.RevitApi;

public class Pieces_ExtractAndFlattenWorkflow(Document doc, string workflowName)
    : MultistepObservableAction<Pieces_ExtractAndFlattenDto, List<DFMAPiece>>(doc, workflowName)
{
    public override void SafelyInitializeInputs(object[] args)
    {
        _dto.OuterLoops = args[0] as List<List<Line>>;
        _dto.VerticalLoops = args[1] as List<List<Line>>;
        _dto.HorizontalLoops = args[2] as List<List<Line>>;
    }

    protected override void SetActions()
    {
        Add(FlattenAndCreatePieces);
        Add(SetResult);
    }

    public void FlattenAndCreatePieces(List<string> _telemetry)
    {
        var allPieces = new List<DFMAPiece>();
        var allLoops = new List<List<Line>>();

        if (_dto.OuterLoops != null) allLoops.AddRange(_dto.OuterLoops);
        if (_dto.VerticalLoops != null) allLoops.AddRange(_dto.VerticalLoops);
        if (_dto.HorizontalLoops != null) allLoops.AddRange(_dto.HorizontalLoops);

        foreach (var loop in allLoops)
        {
            var piece = new DFMAPiece { FlattenedContours = new List<Line>() };
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;

            foreach (var line in loop)
            {
                // Flatten Z to 0
                var p1 = new XYZ(line.GetEndPoint(0).X, line.GetEndPoint(0).Y, 0);
                var p2 = new XYZ(line.GetEndPoint(1).X, line.GetEndPoint(1).Y, 0);

                piece.FlattenedContours.Add(Line.CreateBound(p1, p2));

                // Calculate Bounds
                minX = Math.Min(minX, Math.Min(p1.X, p2.X));
                maxX = Math.Max(maxX, Math.Max(p1.X, p2.X));
                minY = Math.Min(minY, Math.Min(p1.Y, p2.Y));
                maxY = Math.Max(maxY, Math.Max(p1.Y, p2.Y));
            }

            piece.MinX = minX; piece.MaxX = maxX;
            piece.MinY = minY; piece.MaxY = maxY;

            // Shift piece to local origin (0,0) so the nesting algorithm starts clean
            ShiftPieceToOrigin(piece);
            allPieces.Add(piece);
        }
        _dto.FlattenedPieces = allPieces;
    }

    private void ShiftPieceToOrigin(DFMAPiece piece)
    {
        var shiftVector = new XYZ(-piece.MinX, -piece.MinY, 0);
        var shiftedLines = new List<Line>();
        foreach (var line in piece.FlattenedContours)
        {
            shiftedLines.Add(Line.CreateBound(line.GetEndPoint(0) + shiftVector, line.GetEndPoint(1) + shiftVector));
        }
        piece.FlattenedContours = shiftedLines;
        piece.MaxX -= piece.MinX; piece.MinX = 0;
        piece.MaxY -= piece.MinY; piece.MinY = 0;
    }

    public void SetResult(List<string> _telemetry) => Result = _dto.FlattenedPieces;
}

public class Pieces_ExtractAndFlattenDto : Dto
{
    public List<List<Line>> OuterLoops { get; set; }
    public List<List<Line>> VerticalLoops { get; set; }
    public List<List<Line>> HorizontalLoops { get; set; }
    public List<DFMAPiece> FlattenedPieces { get; set; }
}