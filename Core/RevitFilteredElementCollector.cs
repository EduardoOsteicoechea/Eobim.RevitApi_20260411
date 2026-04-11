using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

namespace Eobim.RevitApi.Core;

public static class RevitFilteredElementCollector
{
    public static List<Room> AllRooms(Document doc)
    {
        return new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .OfClass(typeof(SpatialElement))
            .OfCategory(BuiltInCategory.OST_Rooms)
            .Cast<Room>()
            .ToList();
	}
	public static List<T> ByBuiltInCategory<T>(Document doc, BuiltInCategory builtInCategory)
	{
		return new FilteredElementCollector(doc)
			.WhereElementIsNotElementType()
			.OfClass(typeof(T))
			.OfCategory(builtInCategory)
			.Cast<T>()
			.ToList();
	}
}