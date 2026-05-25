using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI.Selection;

namespace Eobim.RevitApi.SelectionFilter;

public class RoomSelectionFilterByName : ISelectionFilter
{
    private readonly string _targetName;

    // 1. Constructor to initialize the filter with the desired substring
    public RoomSelectionFilterByName(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            throw new ArgumentException("Search string cannot be null or empty.", nameof(targetName));
        }

        _targetName = targetName;
    }

    public bool AllowElement(Element element)
    {
        // 2. Pattern matching handles the type check and cast in one step
        if (element is Room room)
        {
            return room.Name.Contains(_targetName, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    public bool AllowReference(Reference reference, XYZ position)
    {
        return false;
    }
}