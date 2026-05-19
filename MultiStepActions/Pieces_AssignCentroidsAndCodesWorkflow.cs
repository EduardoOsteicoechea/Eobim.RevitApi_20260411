//using Autodesk.Revit.DB;
//using Eobim.RevitApi.Commands;
//using Eobim.RevitApi.Framework;

//namespace Eobim.RevitApi;

//public class Pieces_AssignCentroidsAndCodesWorkflow(Document doc, string workflowName)
//    : MultistepObservableAction<Pieces_AssignCentroidsAndCodesDto, List<DFMAPiece>>(doc, workflowName)
//{
//    public override void SafelyInitializeInputs(object[] args)
//    {
//        if (args == null || args.Length == 0)
//            throw new ArgumentException("No pieces provided for Centroid calculation.");

//        _dto.Pieces = args[0] as List<DFMAPiece> ?? throw new ArgumentException("First argument must be List<DFMAPiece>.");
//    }

//    protected override void SetActions()
//    {
//        Add(AssignCodesAndCalculateCentroids);
//        Add(SetResult);
//    }

//    public void AssignCodesAndCalculateCentroids(List<string> _telemetry)
//    {
//        if (_dto.Pieces == null || !_dto.Pieces.Any())
//        {
//            _telemetry.Add("Warning: Piece list is empty. Skipping assignment.");
//            return;
//        }

//        for (int i = 0; i < _dto.Pieces.Count; i++)
//        {
//            var piece = _dto.Pieces[i];

//            // 1. Assign Unique Code
//            // Formats as C-001, C-002, etc. (Padding with zeros keeps laser software sorting clean)
//            piece.UniqueCode = $"C-{(i + 1):D3}";

//            // 2. Calculate True Polygon Centroid
//            if (piece.FlattenedContours != null && piece.FlattenedContours.Any())
//            {
//                piece.Centroid = CalculateTruePolygonCentroid(piece.FlattenedContours);
//            }
//            else
//            {
//                piece.Centroid = XYZ.Zero; // Fallback for invalid geometry
//            }
//        }

//        _telemetry.Add($"Successfully calculated centroids and assigned codes for {_dto.Pieces.Count} pieces.");
//    }

//    /// <summary>
//    /// Calculates the true 2D Center of Mass using the Shoelace/Surveyor's formula.
//    /// This ensures text is placed inside the physical material, not in the empty space of L-shaped or U-shaped cuts.
//    /// </summary>
//    private XYZ CalculateTruePolygonCentroid(List<Line> contours)
//    {
//        double area = 0;
//        double cx = 0;
//        double cy = 0;

//        // Extract vertices assuming the lines form a continuous closed loop
//        var points = contours.Select(c => c.GetEndPoint(0)).ToList();

//        for (int i = 0; i < points.Count; i++)
//        {
//            XYZ p1 = points[i];
//            XYZ p2 = points[(i + 1) % points.Count]; // Wrap around to the first point for the last edge

//            // Shoelace cross product
//            double crossProduct = (p1.X * p2.Y) - (p2.X * p1.Y);

//            area += crossProduct;
//            cx += (p1.X + p2.X) * crossProduct;
//            cy += (p1.Y + p2.Y) * crossProduct;
//        }

//        area *= 0.5;

//        // Failsafe: If the area is practically zero (e.g., a straight line or unclosed loop),
//        // fallback to the simple center of the bounding box.
//        if (Math.Abs(area) < 1e-6)
//        {
//            double minX = points.Min(p => p.X);
//            double maxX = points.Max(p => p.X);
//            double minY = points.Min(p => p.Y);
//            double maxY = points.Max(p => p.Y);
//            return new XYZ((minX + maxX) / 2.0, (minY + maxY) / 2.0, 0);
//        }

//        // Finalize centroid coordinates
//        cx = cx / (6.0 * area);
//        cy = cy / (6.0 * area);

//        return new XYZ(cx, cy, 0);
//    }

//    public void SetResult(List<string> _telemetry)
//    {
//        Result = _dto.Pieces;
//    }
//}

//public class Pieces_AssignCentroidsAndCodesDto : Dto
//{
//    public List<DFMAPiece> Pieces { get; set; }
//}