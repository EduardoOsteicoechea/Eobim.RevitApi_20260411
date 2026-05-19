//using Autodesk.Revit.DB;
//using Eobim.RevitApi.DFMA;
//using Eobim.RevitApi.Framework;

//namespace Eobim.RevitApi.MultiStepActions;

//public record DFMA_GeneratePieceFromLineArgs(
//    Line InputLine,
//    double ContourThickness,
//    ContourWorkplaneAlignmentOptions ContourWorkplaneAlignmentOption,
//    double VerticalContourHeight
//    );

//public class DFMA_GeneratePieceFromLine(Document doc, string parentCommandName)
//:
//MultistepObservableAction<DFMA_GeneratePieceFromLineArgs, DFMA_GeneratePieceFromLineDto, PieceContour>(doc, parentCommandName)
//{
//    public override void SafelyInitializeInputs(DFMA_GeneratePieceFromLineArgs args)
//    {
//        _dto.InputLine = args.InputLine;
//        _dto.ContourThickness = args.ContourThickness;
//        _dto.ContourWorkplaneAlignmentOption = args.ContourWorkplaneAlignmentOption;
//        _dto.VerticalContourHeight = args.VerticalContourHeight;
//    }

//    protected override void SetActions()
//    {
//        Add(SetContourLinesPerpendicularDisplacement);
//        Add(GenerateCurveLoopsFromOrderedOffsetLines);
//        Add(SetResult);
//    }

//    public void SetContourLinesPerpendicularDisplacement(List<string> _telemetry)
//    {
//        _dto.ContourLinesPerpendicularDisplacement = _dto.ContourThickness / 2;
//    }

//    //public void GenerateCurveLoopsFromOrderedOffsetLines(List<string> _telemetry)
//    //{
//    //    var result = new PieceContour
//    //    {
//    //        ContourWorkplaneAlignmentOption = _dto.ContourWorkplaneAlignmentOption
//    //    };

//    //    var line = _dto.InputLine;

//    //    result.RotationPoint = _dto.InputLine.GetEndPoint(0);

//    //    var displacement = _dto.ContourLinesPerpendicularDisplacement;

//    //    List<Line> lineContour = new List<Line>();

//    //    _telemetry.Add($"Processing line from {line.GetEndPoint(0)} to {line.GetEndPoint(1)} with direction {line.Direction}");
//    //    _telemetry.Add($"{nameof(result.ContourWorkplaneAlignmentOption)}: {result.ContourWorkplaneAlignmentOption}");

//    //    result.ContourInitialLineDirection = line.Direction;

//    //    var linePerpendicularDirection = line.Direction.CrossProduct(XYZ.BasisZ).Normalize();

//    //    if (result.ContourWorkplaneAlignmentOption.Equals(ContourWorkplaneAlignmentOptions.ZAndCustomAngle))
//    //    {
//    //        result.ContourPrintXRotation = line.Direction.AngleTo(XYZ.BasisX);
//    //        result.ContourPrintYRotation = 0;
//    //        result.ContourPrintZRotation = line.Direction.AngleTo(XYZ.BasisZ);

//    //        lineContour = OriginZAlignedContour(line, linePerpendicularDirection, displacement, _dto.VerticalContourHeight);
//    //    }
//    //    else
//    //    {
//    //        result.ContourPrintZRotation = line.Direction.AngleTo(XYZ.BasisX);
//    //        result.ContourPrintXRotation = 0;
//    //        result.ContourPrintYRotation = 0;

//    //        lineContour = OriginXYAlignedContour(line, linePerpendicularDirection, displacement);
//    //    }

//    //    if (lineContour is null) throw new ArgumentNullException(nameof(lineContour));

//    //    result.ContourLines = lineContour;

//    //    _dto.PieceContour = result;
//    //}

//    //public void GenerateCurveLoopsFromOrderedOffsetLines(List<string> _telemetry)
//    //{
//    //    var result = new PieceContour
//    //    {
//    //        ContourWorkplaneAlignmentOption = _dto.ContourWorkplaneAlignmentOption
//    //    };

//    //    var line = _dto.InputLine;
//    //    result.RotationPoint = line.GetEndPoint(0);
//    //    var displacement = _dto.ContourLinesPerpendicularDisplacement;
//    //    List<Line> lineContour = new List<Line>();

//    //    _telemetry.Add($"Processing line from {line.GetEndPoint(0)} to {line.GetEndPoint(1)} with direction {line.Direction}");

//    //    result.ContourInitialLineDirection = line.Direction;
//    //    var linePerpendicularDirection = line.Direction.CrossProduct(XYZ.BasisZ).Normalize();

//    //    // Safely calculate the 360-degree heading of the line in the XY plane
//    //    double headingAngle = Math.Atan2(line.Direction.Y, line.Direction.X);

//    //    if (result.ContourWorkplaneAlignmentOption.Equals(ContourWorkplaneAlignmentOptions.ZAndCustomAngle))
//    //    {
//    //        // 1. Spin it to align perfectly with the X-axis (undo the heading)
//    //        result.ContourPrintZRotation = -headingAngle;

//    //        // 2. Knock it over exactly 90 degrees to lay it flat on the floor
//    //        result.ContourPrintXRotation = Math.PI / 2;

//    //        // 3. No Y rotation needed
//    //        result.ContourPrintYRotation = 0;

//    //        lineContour = OriginZAlignedContour(line, linePerpendicularDirection, displacement, _dto.VerticalContourHeight);
//    //    }
//    //    else
//    //    {
//    //        // Horizontal pieces (XY) are already flat. 
//    //        result.ContourPrintXRotation = 0;
//    //        result.ContourPrintYRotation = 0;

//    //        // We just spin them to align with the X-axis so they pack neatly on the printing bed
//    //        result.ContourPrintZRotation = -headingAngle;

//    //        lineContour = OriginXYAlignedContour(line, linePerpendicularDirection, displacement);
//    //    }

//    //    if (lineContour is null) throw new ArgumentNullException(nameof(lineContour));

//    //    result.ContourLines = lineContour;
//    //    _dto.PieceContour = result;
//    //}

//    //public void GenerateCurveLoopsFromOrderedOffsetLines(List<string> _telemetry)
//    //{
//    //    var result = new PieceContour
//    //    {
//    //        ContourWorkplaneAlignmentOption = _dto.ContourWorkplaneAlignmentOption
//    //    };

//    //    var line = _dto.InputLine;
//    //    var displacement = _dto.ContourLinesPerpendicularDisplacement;
//    //    List<Line> lineContour = new List<Line>();

//    //    _telemetry.Add($"Processing line from {line.GetEndPoint(0)} to {line.GetEndPoint(1)} with direction {line.Direction}");

//    //    result.ContourInitialLineDirection = line.Direction;
//    //    var linePerpendicularDirection = line.Direction.CrossProduct(XYZ.BasisZ).Normalize();

//    //    double headingAngle = Math.Atan2(line.Direction.Y, line.Direction.X);

//    //    if (result.ContourWorkplaneAlignmentOption.Equals(ContourWorkplaneAlignmentOptions.ZAndCustomAngle))
//    //    {
//    //        result.ContourPrintZRotation = -headingAngle;
//    //        result.ContourPrintXRotation = Math.PI / 2;
//    //        result.ContourPrintYRotation = 0;

//    //        lineContour = OriginZAlignedContour(line, linePerpendicularDirection, displacement, _dto.VerticalContourHeight);
//    //    }
//    //    else
//    //    {
//    //        result.ContourPrintXRotation = 0;
//    //        result.ContourPrintYRotation = 0;
//    //        result.ContourPrintZRotation = -headingAngle;

//    //        lineContour = OriginXYAlignedContour(line, linePerpendicularDirection, displacement);
//    //    }

//    //    if (lineContour is null) throw new ArgumentNullException(nameof(lineContour));

//    //    result.ContourLines = lineContour;

//    //    // =========================================================================
//    //    // THE MAGIC: Calculate True Flattened Bounding Box Anchor
//    //    // =========================================================================
//    //    // 1. Simulate the exact rotations that will be applied during placement
//    //    Transform rotX = Transform.CreateRotation(XYZ.BasisX, result.ContourPrintXRotation);
//    //    Transform rotY = Transform.CreateRotation(XYZ.BasisY, result.ContourPrintYRotation);
//    //    Transform rotZ = Transform.CreateRotation(XYZ.BasisZ, result.ContourPrintZRotation);

//    //    // Apply Z, then Y, then X
//    //    Transform rotationTransform = rotX.Multiply(rotY).Multiply(rotZ);

//    //    double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;

//    //    // 2. Transform the contour points and find the flattened minimums
//    //    foreach (var edge in lineContour)
//    //    {
//    //        for (int i = 0; i <= 1; i++)
//    //        {
//    //            XYZ pt = edge.GetEndPoint(i);
//    //            XYZ rotatedPt = rotationTransform.OfPoint(pt); // Simulate flattening

//    //            if (rotatedPt.X < minX) minX = rotatedPt.X;
//    //            if (rotatedPt.Y < minY) minY = rotatedPt.Y;
//    //            if (rotatedPt.Z < minZ) minZ = rotatedPt.Z;
//    //        }
//    //    }

//    //    // 3. Map the flattened Min corner back to its original 3D coordinate using the Inverse Transform
//    //    XYZ flattenedMin = new XYZ(minX, minY, minZ);
//    //    XYZ originalSpaceAnchor = rotationTransform.Inverse.OfPoint(flattenedMin);

//    //    // 4. Set this exact coordinate as the RotationPoint! 
//    //    result.RotationPoint = originalSpaceAnchor;

//    //    _dto.PieceContour = result;
//    //}
//    public void GenerateCurveLoopsFromOrderedOffsetLines(List<string> _telemetry)
//    {
//        var result = new PieceContour
//        {
//            ContourWorkplaneAlignmentOption = _dto.ContourWorkplaneAlignmentOption
//        };

//        var line = _dto.InputLine;
//        var displacement = _dto.ContourLinesPerpendicularDisplacement;
//        List<Line> lineContour = new List<Line>();

//        _telemetry.Add($"Processing line from {line.GetEndPoint(0)} to {line.GetEndPoint(1)} with direction {line.Direction}");

//        result.ContourInitialLineDirection = line.Direction;
//        var linePerpendicularDirection = line.Direction.CrossProduct(XYZ.BasisZ).Normalize();

//        // Safely calculate the 360-degree heading of the line in the XY plane
//        double headingAngle = Math.Atan2(line.Direction.Y, line.Direction.X);

//        if (result.ContourWorkplaneAlignmentOption.Equals(ContourWorkplaneAlignmentOptions.ZAndCustomAngle))
//        {
//            result.ContourPrintZRotation = -headingAngle;
//            result.ContourPrintXRotation = Math.PI / 2;
//            result.ContourPrintYRotation = 0;

//            lineContour = OriginZAlignedContour(line, linePerpendicularDirection, displacement, _dto.VerticalContourHeight);
//        }
//        else
//        {
//            result.ContourPrintXRotation = 0;
//            result.ContourPrintYRotation = 0;
//            result.ContourPrintZRotation = -headingAngle;

//            lineContour = OriginXYAlignedContour(line, linePerpendicularDirection, displacement);
//        }

//        if (lineContour is null) throw new ArgumentNullException(nameof(lineContour));

//        result.ContourLines = lineContour;

//        // =========================================================================
//        // TRUE BOUNDING BOX ANCHOR CALCULATION
//        // =========================================================================
//        Transform rotX = Transform.CreateRotation(XYZ.BasisX, result.ContourPrintXRotation);
//        Transform rotY = Transform.CreateRotation(XYZ.BasisY, result.ContourPrintYRotation);
//        Transform rotZ = Transform.CreateRotation(XYZ.BasisZ, result.ContourPrintZRotation);

//        Transform rotationTransform = rotX.Multiply(rotY).Multiply(rotZ);

//        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;

//        // Simulate the flattening to find the absolute minimum corner
//        foreach (var edge in lineContour)
//        {
//            for (int i = 0; i <= 1; i++)
//            {
//                XYZ pt = edge.GetEndPoint(i);
//                XYZ rotatedPt = rotationTransform.OfPoint(pt);

//                if (rotatedPt.X < minX) minX = rotatedPt.X;
//                if (rotatedPt.Y < minY) minY = rotatedPt.Y;
//                if (rotatedPt.Z < minZ) minZ = rotatedPt.Z;
//            }
//        }

//        // Map the flattened minimum corner back to the original 3D space
//        XYZ flattenedMin = new XYZ(minX, minY, minZ);
//        XYZ originalSpaceAnchor = rotationTransform.Inverse.OfPoint(flattenedMin);

//        // Set this as the ultimate translation reference!
//        result.RotationPoint = originalSpaceAnchor;

//        _dto.PieceContour = result;
//    }

//    private static List<Line> OriginZAlignedContour(Line line, XYZ linePerpendicularDirection, double displacement, double verticalContourHeight)
//    {
//        var result = new List<Line>();

//        var lineP1 = line.GetEndPoint(0);
//        var lineP2 = line.GetEndPoint(1);

//        var displacedLineP1 = new XYZ(lineP1.X, lineP1.Y, lineP1.Z) - (linePerpendicularDirection * displacement);
//        var displacedLineP2 = new XYZ(lineP2.X, lineP2.Y, lineP2.Z) - (linePerpendicularDirection * displacement);

//        var displacedTopLineP1 = new XYZ(displacedLineP1.X, displacedLineP1.Y, displacedLineP1.Z + verticalContourHeight);
//        var displacedTopLineP2 = new XYZ(displacedLineP2.X, displacedLineP2.Y, displacedLineP2.Z + verticalContourHeight);

//        // 1. Bottom edge: P1 -> P2
//        var bottomEdge = Line.CreateBound(displacedLineP1, displacedLineP2);

//        // 2. Right vertical edge: P2 -> TopP2 (Connects to end of bottom edge)
//        var rightEdge = Line.CreateBound(displacedLineP2, displacedTopLineP2);

//        // 3. Top edge: TopP2 -> TopP1 (Connects to end of right edge, draws backwards)
//        var topEdge = Line.CreateBound(displacedTopLineP2, displacedTopLineP1);

//        // 4. Left vertical edge: TopP1 -> P1 (Connects to end of top edge, closes back to start)
//        var leftEdge = Line.CreateBound(displacedTopLineP1, displacedLineP1);

//        // Add them in the exact order they were drawn
//        result.Add(bottomEdge);
//        result.Add(rightEdge);
//        result.Add(topEdge);
//        result.Add(leftEdge);

//        return result;
//    }

//    private static List<Line> OriginXYAlignedContour(Line line, XYZ linePerpendicularDirection, double displacement)
//    {
//        var result = new List<Line>();

//        var lineP1 = line.GetEndPoint(0);
//        var lineP2 = line.GetEndPoint(1);

//        var pA = new XYZ(lineP1.X, lineP1.Y, lineP1.Z) - (linePerpendicularDirection * displacement);
//        var pB = new XYZ(lineP2.X, lineP2.Y, lineP2.Z) - (linePerpendicularDirection * displacement);
//        var pC = new XYZ(lineP2.X, lineP2.Y, lineP2.Z) + (linePerpendicularDirection * displacement);
//        var pD = new XYZ(lineP1.X, lineP1.Y, lineP1.Z) + (linePerpendicularDirection * displacement);

//        // Create the 4 lines head-to-tail: A->B, B->C, C->D, D->A
//        var side1 = Line.CreateBound(pA, pB);
//        var endCap1 = Line.CreateBound(pB, pC);
//        var side2 = Line.CreateBound(pC, pD);
//        var endCap2 = Line.CreateBound(pD, pA);

//        // Add them to the contour in the exact sequence they were created
//        result.Add(side1);
//        result.Add(endCap1);
//        result.Add(side2);
//        result.Add(endCap2);

//        return result;
//    }

//    public void SetResult(List<string> _telemetry)
//    {
//        Result = _dto.PieceContour;
//    }
//}

//public class DFMA_GeneratePieceFromLineDto : Dto
//{
//    [Print(nameof(TypeFormatter.LineList))]
//    public Line InputLine { get; set; }


//    [Print(nameof(TypeFormatter.Double))]
//    public double ContourThickness { get; set; }


//    //[Print(nameof(TypeFormatter.String))]
//    public ContourWorkplaneAlignmentOptions ContourWorkplaneAlignmentOption { get; set; }


//    [Print(nameof(TypeFormatter.Double))]
//    public double VerticalContourHeight { get; set; }


//    [Print(nameof(TypeFormatter.Double))]
//    public double ContourLinesPerpendicularDisplacement { get; set; }


//    //[Print(nameof(TypeFormatter.LineListList))]
//    public PieceContour PieceContour { get; set; }
//}
