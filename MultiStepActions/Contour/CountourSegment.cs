using Autodesk.Revit.DB;

namespace Eobim.RevitApi.MultiStepActions.Contour;

public class CountourSegment
{
    public bool? IsASingleLine => Lines.Count.Equals(1);
    public List<Line> Lines { get; set; }
}