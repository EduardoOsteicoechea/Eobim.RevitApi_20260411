using Autodesk.Revit.DB;
using Eobim.RevitApi.DFMA;

namespace Eobim.RevitApi.MultiStepActions;

public class DirectShapeDMFAData
{
    public List<Line> BoundaryLines { get; set; }
    public XYZ ExtrusionDirection { get; set; }
    public double ExtrusionThickness { get; set; }
    public DirectShape DirectShape { get; set; }
    public Face DirectBottomFace { get; set; }
    public Line PrintFaceLine { get; set; }
    public double MinX { get; set; }
    public double MinY { get; set; }
    public double MaxX { get; set; }
    public double MaxY { get; set; }
    public Face DirectShapeLeadFace { get; set; }
    public Reference DirectShapeLeadFaceReference { get; set; }

    // From here bellow, these properties must be filled in the 
    // Dynamic placement algorithm
    public XYZ PlacementPoint { get; set; }
    public double RequiredXDisplacement { get; set; }
    public double RequiredYDisplacement { get; set; }
    public double RequiredZDisplacement { get; set; }
    public CurveLoop DisplacedCurveLoop { get; set; }
    public double AngleToXYZBasisZ { get; set; }
    public PieceContour PieceContour { get; set; }

    public DirectShapeDMFAData() {}
    public DirectShapeDMFAData(DirectShape directShape)
    {
        DirectShape = directShape;
        BoundaryLines = new List<Line>();

        // 1. Extract the Geometry and Solid from the DirectShape
        var options = new Options { ComputeReferences = true, DetailLevel = ViewDetailLevel.Fine };
        var geometryElement = directShape.get_Geometry(options);

        // Find the first solid with volume
        var solid = geometryElement?.OfType<Solid>().FirstOrDefault(s => s.Volume > 0);
        if (solid == null) return;

        var faces = solid.Faces.Cast<Face>().ToList();

        // 2. Extract Bottom Face (looking straight down at the floor)
        DirectBottomFace = faces.FirstOrDefault(a =>
        {
            var faceNormal = a.ComputeNormal(new UV(.5, .5));
            return faceNormal.IsAlmostEqualTo(XYZ.BasisZ.Negate());
        });

        // 3. Infer Extrusion Direction and Extract Lead Face
        // Assuming standard vertical extrusion based on the reference class behavior
        ExtrusionDirection = XYZ.BasisZ;
        AngleToXYZBasisZ = ExtrusionDirection.AngleTo(XYZ.BasisZ); // Will evaluate to 0.0

        DirectShapeLeadFace = faces.FirstOrDefault(a =>
        {
            var faceNormal = a.ComputeNormal(new UV(.5, .5));
            return faceNormal.IsAlmostEqualTo(ExtrusionDirection);
        });

        DirectShapeLeadFaceReference = DirectShapeLeadFace?.Reference;

        // 4. Extract Boundary Lines and Enclosing Dimensions from the Bottom Face
        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;

        if (DirectBottomFace != null)
        {
            var edgeLoops = DirectBottomFace.EdgeLoops;
            if (edgeLoops != null && !edgeLoops.IsEmpty)
            {
                var primaryLoop = edgeLoops.get_Item(0); // Use the outer boundary loop
                foreach (Edge edge in primaryLoop)
                {
                    var curve = edge.AsCurve();
                    if (curve is Line line)
                    {
                        BoundaryLines.Add(line);

                        var p1 = line.GetEndPoint(0);
                        var p2 = line.GetEndPoint(1);

                        // Calculate Enclosing Dimensions (matching reference logic)
                        if (p1.X < minX) minX = p1.X;
                        if (p2.X < minX) minX = p2.X;

                        if (p1.X > maxX) maxX = p1.X;
                        if (p2.X > maxX) maxX = p2.X;

                        if (p1.Y < minY) minY = p1.Y;
                        if (p2.Y < minY) minY = p2.Y;

                        if (p1.Y > maxY) maxY = p1.Y;
                        if (p2.Y > maxY) maxY = p2.Y;
                    }
                }
            }
        }

        // Assign Dimensions (with safety fallbacks in case of missing geometry)
        MinX = minX != double.MaxValue ? minX : 0;
        MinY = minY != double.MaxValue ? minY : 0;
        MaxX = maxX != double.MinValue ? maxX : 0;
        MaxY = maxY != double.MinValue ? maxY : 0;

        // 5. Calculate Extrusion Thickness
        if (DirectBottomFace is PlanarFace bottomPlanar && DirectShapeLeadFace is PlanarFace topPlanar)
        {
            // Thickness is the absolute Z distance between the top and bottom faces
            ExtrusionThickness = Math.Abs(topPlanar.Origin.Z - bottomPlanar.Origin.Z);
        }
        else
        {
            // Fallback: Utilize the DirectShape bounding box if planar faces couldn't be evaluated
            var bbox = directShape.get_BoundingBox(null);
            if (bbox != null)
            {
                ExtrusionThickness = bbox.Max.Z - bbox.Min.Z;
            }
        }
    }
}
