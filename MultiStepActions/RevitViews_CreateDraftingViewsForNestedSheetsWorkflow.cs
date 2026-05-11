using Autodesk.Revit.DB;
using Eobim.RevitApi.Commands;
using Eobim.RevitApi.Framework;

namespace Eobim.RevitApi;

public class RevitViews_CreateDraftingViewsForNestedSheetsWorkflow(Document doc, string workflowName)
    : MultistepObservableAction<RevitViews_CreateDraftingViewsDto, List<ViewDrafting>>(doc, workflowName)
{
    public override void SafelyInitializeInputs(object[] args)
    {
        if (args == null || args.Length == 0)
            throw new ArgumentException("Nested sheets must be provided.");

        _dto.Sheets = args[0] as List<DFMANestedSheet> ?? throw new ArgumentException("First argument must be List<DFMANestedSheet>.");
    }

    protected override void SetActions()
    {
        Add(FetchRequiredRevitTypes);
        Add(CreateViewsAndDrawElements);
        Add(SetResult);
    }

    public void FetchRequiredRevitTypes(List<string> _telemetry)
    {
        // 1. Get the Drafting View Family Type
        _dto.DraftingViewFamilyTypeId = new FilteredElementCollector(_doc)
            .OfClass(typeof(ViewFamilyType))
            .Cast<ViewFamilyType>()
            .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.Drafting)?.Id;

        if (_dto.DraftingViewFamilyTypeId == null)
        {
            throw new InvalidOperationException("No Drafting View Family Type found in the document.");
        }

        // 2. Get a Default Text Note Type for the piece codes
        _dto.DefaultTextTypeId = new FilteredElementCollector(_doc)
            .OfClass(typeof(TextNoteType))
            .FirstElementId();

        if (_dto.DefaultTextTypeId == null)
        {
            _telemetry.Add("Warning: No TextNoteType found. Text labels will not be generated.");
        }
    }

    public void CreateViewsAndDrawElements(List<string> _telemetry)
    {
        _dto.DraftingViews = new List<ViewDrafting>();

        foreach (var sheet in _dto.Sheets)
        {
            var draftingView = ViewDrafting.Create(_doc, _dto.DraftingViewFamilyTypeId);

            draftingView.Name = $"DFMA_Assembly_Sheet_{sheet.SheetNumber}_{Guid.NewGuid().ToString().Substring(0, 4)}";
            draftingView.Scale = 10;

            // Map nest plane (world X,Y with Z=0 from flatten/nest) onto this view's sketch plane.
            var nestPlaneToSketchTransform = BuildNestXyAtZeroToDraftingSketchTransform(draftingView);

            int lineCount = 0;
            int textCount = 0;

            foreach (var piece in sheet.PlacedPieces)
            {
                foreach (var line in piece.FlattenedContours)
                {
                    var transformedLine = TransformNestLineToSketchPlane(line, nestPlaneToSketchTransform);
                    _doc.Create.NewDetailCurve(draftingView, transformedLine);
                    lineCount++;
                }

                if (!string.IsNullOrEmpty(piece.UniqueCode) && piece.Centroid != null && _dto.DefaultTextTypeId != null)
                {
                    var textOpts = new TextNoteOptions(_dto.DefaultTextTypeId)
                    {
                        HorizontalAlignment = HorizontalTextAlignment.Center,
                        VerticalAlignment = VerticalTextAlignment.Middle
                    };

                    var centroidOnSketch = TransformNestPointToSketchPlane(piece.Centroid, nestPlaneToSketchTransform);
                    TextNote.Create(_doc, draftingView.Id, centroidOnSketch, piece.UniqueCode, textOpts);
                    textCount++;
                }
            }

            _telemetry.Add($"Drafting View '{draftingView.Name}' created with {lineCount} lines and {textCount} text labels.");
            _dto.DraftingViews.Add(draftingView);
        }
    }

    /// <summary>
    /// Builds a rigid transform: local (nestX, nestY, nestZ) maps to
    /// <c>Origin + nestX * RightDirection + nestY * UpDirection + nestZ * ViewNormal</c>,
    /// using the drafting view's sketch axes. Nest data uses world (X,Y,0); we only use X,Y as
    /// coordinates along the view's horizontal and vertical paper directions so the layout lies flat in the view.
    /// </summary>
    private static Transform BuildNestXyAtZeroToDraftingSketchTransform(ViewDrafting view)
    {
        var basisX = view.RightDirection.Normalize();
        var basisY = view.UpDirection.Normalize();
        var basisZ = basisX.CrossProduct(basisY).Normalize();

        var t = Transform.Identity;
        t.Origin = view.Origin;
        t.BasisX = basisX;
        t.BasisY = basisY;
        t.BasisZ = basisZ;
        return t;
    }

    /// <summary>
    /// Applies the nest-to-sketch transform: nest point uses <see cref="XYZ.X"/> and <see cref="XYZ.Y"/>
    /// as 2D nest coordinates (world flatten layout); <see cref="XYZ.Z"/> is carried through (typically 0).
    /// </summary>
    private static XYZ TransformNestPointToSketchPlane(XYZ nestPlanePoint, Transform nestPlaneToSketch)
    {
        return nestPlaneToSketch.Origin
            + nestPlaneToSketch.BasisX * nestPlanePoint.X
            + nestPlaneToSketch.BasisY * nestPlanePoint.Y
            + nestPlaneToSketch.BasisZ * nestPlanePoint.Z;
    }

    private static Line TransformNestLineToSketchPlane(Line nestLine, Transform nestPlaneToSketch)
    {
        var a = TransformNestPointToSketchPlane(nestLine.GetEndPoint(0), nestPlaneToSketch);
        var b = TransformNestPointToSketchPlane(nestLine.GetEndPoint(1), nestPlaneToSketch);
        return Line.CreateBound(a, b);
    }

    public void SetResult(List<string> _telemetry)
    {
        Result = _dto.DraftingViews;
    }
}

public class RevitViews_CreateDraftingViewsDto : Dto
{
    public List<DFMANestedSheet> Sheets { get; set; }
    public ElementId DraftingViewFamilyTypeId { get; set; }
    public ElementId DefaultTextTypeId { get; set; }
    public List<ViewDrafting> DraftingViews { get; set; }
}
