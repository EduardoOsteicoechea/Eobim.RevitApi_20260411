using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI.Selection;

namespace Eobim.RevitApi.SelectionFilter;

public class RoomSelectionFilter : ISelectionFilter
{
    public bool AllowElement(Element element)
    {
        if (element is Room)
        {
            return true;
        }

        if (
            element.Category is not null
            &&
            element.Category.Id.Value.Equals((int)BuiltInCategory.OST_Rooms)
        )
        {
            return true;
        }

        return false;
    }

    public bool AllowReference(Reference reference, XYZ position)
    {
        return false;
    }
}
