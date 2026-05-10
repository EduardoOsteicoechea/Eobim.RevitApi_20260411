using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Security;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.Exceptions; // Added for InvalidObjectException

namespace Eobim.RevitApi.Framework;

public static class TypeFormatter
{
    // ==========================================
    // SAFTEY SHIELDS
    // ==========================================

    // Shield for standard Elements (Walls, Floors, Families, etc.)
    private static string SafeElement<T>(T item, Func<T, string> readFunc) where T : Element
    {
        if (item == null) return "[null]";
        if (!item.IsValidObject) return $"[Invalid/Rolled Back {item.GetType().Name}]";
        return readFunc(item);
    }

    // Shield for Unmanaged Geometry / Wrappers (Face, Solid, Line, etc.)
    [HandleProcessCorruptedStateExceptions]
    [SecurityCritical]
    private static string SafeReadGeometry<T>(T data, Func<T, string> readFunc, string objectName)
    {
        if (data == null) return "[null]";
        try
        {
            return readFunc(data);
        }
        catch (InvalidObjectException)
        {
            return $"[Invalid/Rolled Back {objectName}]";
        }
        catch (AccessViolationException)
        {
            return $"[Dead C++ {objectName} Pointer]";
        }
        catch (Exception ex)
        {
            return $"[Error reading {objectName}: {ex.Message}]";
        }
    }

    // ==========================================
    // FORMATTERS
    // ==========================================

    public static object ElementIdSolidTuple(List<(ElementId, Solid)> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"[");

        foreach ((ElementId, Solid) item in data)
        {
            printer.Append($"({ElementId(item.Item1)}, {Solid(item.Item2)}),");
        }

        printer.Append($"]");

        return printer.ToString();
    }

    public static object ElementIdIDtoDictionary(Dictionary<ElementId, IDto> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"[");

        foreach (KeyValuePair<ElementId, IDto> item in data)
        {
            var properties = item.Value.ToObservableObject();
            var formattedProps = string.Join(", ", properties.Select(p => $"{p.Item1}: {p.Item2}"));
            printer.Append($"({ElementId(item.Key)}, [{formattedProps}]),");
        }

        printer.Append($"]");

        return printer.ToString();
    }

    public static object IDto(IDto data)
    {
        if (data == null) return "[null]";

        return data.ToObservableObject();
    }
    public static object IDtos(List<IDto> data)
    {
        if (data == null) return "[null]";

        return data.Select(x => IDto(x)).ToList();
    }

    public static string BoundarySegmentListOfList(List<List<BoundarySegment>> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"[{data.Count}], ");
        printer.Append($"[");

        foreach (List<BoundarySegment> item in data)
        {
            printer.Append($"[{item.Count}], ");
            printer.Append($"[");
            foreach (BoundarySegment item1 in item)
            {
                printer.Append($"{BoundarySegment(item1)}, ");
            }
            printer.Append($"], ");
        }

        printer.Append($"]");

        return printer.ToString();
    }

    public static string BoundarySegment(BoundarySegment data)
    {
        return SafeReadGeometry(data, d => $"{d.ElementId.ToString()}", "BoundarySegment");
    }

    public static string BoundarySegmentList(List<BoundarySegment> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"({data.Count}, ");
        printer.Append($"(");

        foreach (BoundarySegment item in data)
        {
            printer.Append($"{BoundarySegment(item)}, ");
        }

        printer.Append($")");
        printer.Append($")");

        return printer.ToString();
    }

    public static string ElementIdBoundarySegmentListDictionary(Dictionary<ElementId, List<BoundarySegment>> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"(");

        foreach (KeyValuePair<ElementId, List<BoundarySegment>> item in data)
        {
            printer.Append($"(");
            printer.Append($"{ElementId(item.Key)}, ");
            printer.Append($"{BoundarySegmentList(item.Value)}");
            printer.Append($"), ");
        }

        printer.Append($")");

        return printer.ToString();
    }

    public static string WallList(List<Wall> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"[");
        printer.Append($"({data.Count}), ");
        printer.Append($"(");

        foreach (Wall item in data)
        {
            printer.Append($"{Wall(item)}, ");
        }

        printer.Append($")");
        printer.Append($"]");

        return printer.ToString();
    }

    public static string ElementIdWallListDictionary(Dictionary<ElementId, List<Wall>> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"(");

        foreach (KeyValuePair<ElementId, List<Wall>> item in data)
        {
            printer.Append($"(");
            printer.Append($"{ElementId(item.Key)}, ");
            printer.Append($"{WallList(item.Value)}");
            printer.Append($"), ");
        }

        printer.Append($")");

        return printer.ToString();
    }

    public static string ElementIdStringListDictionary(Dictionary<ElementId, List<string>> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"(");

        foreach (KeyValuePair<ElementId, List<string>> item in data)
        {
            printer.Append($"(");
            printer.Append($"{ElementId(item.Key)}, ");
            printer.Append($"(");
            printer.Append($"{string.Join(", ", item.Value)}");
            printer.Append($")");
            printer.Append($"), ");
        }

        printer.Append($")");

        return printer.ToString();
    }

    public static string ElementIdStringIEnumerableDictionary(Dictionary<ElementId, IEnumerable<string>> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"(");

        foreach (KeyValuePair<ElementId, IEnumerable<string>> item in data)
        {
            printer.Append($"(");
            printer.Append($"{ElementId(item.Key)}, ");
            printer.Append($"(");
            printer.Append($"{string.Join(", ", item.Value)}");
            printer.Append($")");
            printer.Append($"), ");
        }

        printer.Append($")");

        return printer.ToString();
    }

    public static string ElementIdReferenceListDictionary(Dictionary<ElementId, List<Reference>> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"(");

        foreach (KeyValuePair<ElementId, List<Reference>> item in data)
        {
            printer.Append($"(");
            printer.Append($"{ElementId(item.Key)}, ");
            printer.Append($"{ReferenceList(item.Value)}");
            printer.Append($"), ");
        }

        printer.Append($")");

        return printer.ToString();
    }

    public static string ReferenceList(List<Reference> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"(");

        foreach (Reference item in data)
        {
            printer.Append($"{Reference(item)}, ");
        }

        printer.Append($")");

        return printer.ToString();
    }

    public static string Reference(Reference data)
    {
        return SafeReadGeometry(data, d => $"{d.ElementId}", "Reference");
    }

    public static string ReferencePlanes(List<ReferencePlane> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"[");

        foreach (ReferencePlane item in data)
        {
            printer.Append($"({ReferencePlane(item)}), ");
        }

        printer.Append($"]");

        return printer.ToString();
    }

    public static string ReferencePlane(ReferencePlane data)
    {
        return SafeElement(data, d => $"{d.Id.ToString()}: {d.Name}");
    }

    public static string CurveList(List<Curve> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"[");

        foreach (Curve item in data)
        {
            printer.Append($"({Curve(item)}), ");
        }

        printer.Append($"]");

        return printer.ToString();
    }

    public static string Curve(Curve data)
    {
        return SafeReadGeometry(data, d => $"{d.ApproximateLength}", "Curve");
    }

    public static string CurveRich(Curve data)
    {
        return SafeReadGeometry(data, d => $"(Is Line:{d is Line} | {nameof(d.ApproximateLength)}: {d.ApproximateLength} | GetEndPoint(0): {d.GetEndPoint(0)} | GetEndPoint(1): {d.GetEndPoint(1)})", "Curve");
    }

    public static string CurveLoop(CurveLoop data)
    {
        return SafeReadGeometry(data, d =>
        {
            var lines = d.ToList();
            var printer = new StringBuilder();
            printer.Append($"({lines.Count} | {nameof(d.IsOpen)}: {d.IsOpen()} | {nameof(d.IsCounterclockwise)}: {d.IsCounterclockwise(Autodesk.Revit.DB.XYZ.BasisZ)} | [");

            foreach (Curve item in lines)
            {
                printer.Append($"{CurveRich(item)}, ");
            }

            printer.Append($"]");
            return printer.ToString();
        }, "CurveLoop");
    }

    public static string LineList(List<Line> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"[");

        foreach (Line item in data)
        {
            printer.Append($"({Line(item)}), ");
        }

        printer.Append($"]");

        return printer.ToString();
    }

    public static string LineListList(List<List<Line>> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"[");

        foreach (List<Line> item in data)
        {
            printer.Append($"({LineList(item)}), ");
        }

        printer.Append($"]");

        return printer.ToString();
    }

    public static string Line(Line data)
    {
        return SafeReadGeometry(data, d => $"{d.ApproximateLength}", "Line");
    }

    public static string Walls(List<Wall> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"[");

        foreach (Wall item in data)
        {
            printer.Append($"({Wall(item)}), ");
        }

        printer.Append($"]");

        return printer.ToString();
    }

    public static string Wall(Wall data)
    {
        return SafeElement(data, d => $"{d.Id.ToString()}: {d.Name}");
    }

    public static string FloorList(List<Floor> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"({data.Count}, [");

        foreach (Floor item in data)
        {
            printer.Append($"{Floor(item)}, ");
        }

        printer.Append($"]");

        return printer.ToString();
    }

    public static string Floor(Floor data)
    {
        return SafeElement(data, d => $"{d.Id.ToString()} | {d.Name}");
    }

    public static string Levels(List<Level> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"[");

        foreach (Level item in data)
        {
            printer.Append($"({Level(item)}), ");
        }

        printer.Append($"]");

        return printer.ToString();
    }

    public static string Level(Level data)
    {
        return SafeElement(data, d => $"{d.Id.ToString()} | {d.Name} | {d.Elevation}");
    }

    public static string Elements(List<Element> items)
    {
        if (items == null) return "[null]";

        var data = new StringBuilder();

        data.Append($"[");

        foreach (Element item in items)
        {
            data.Append($"{Element(item)}, ");
        }

        data.Append($"]");

        return data.ToString();
    }

    public static string BuiltInCategoryList(List<BuiltInCategory> items)
    {
        if (items == null) return "[null]";

        var data = new StringBuilder();

        data.Append($"[");

        foreach (BuiltInCategory item in items)
        {
            data.Append($"{BuiltInCategory(item)}, ");
        }

        data.Append($"]");

        return data.ToString();
    }

    public static string BuiltInCategory(BuiltInCategory data)
    {
        // Enums are safe
        return $"{data.ToString()}";
    }

    public static string ElementIdList(List<ElementId> items)
    {
        if (items == null) return "[null]";

        var data = new StringBuilder();

        data.Append($"[");

        foreach (ElementId item in items)
        {
            data.Append($"{ElementId(item)}, ");
        }

        data.Append($"]");

        return data.ToString();
    }

    public static string Elements<T>(List<T> items) where T : Element
    {
        if (items == null) return "[null]";

        var data = new StringBuilder();

        data.Append($"[");

        foreach (T item in items)
        {
            data.Append($"{Element(item)}, ");
        }

        data.Append($"]");

        return data.ToString();
    }

    public static string AreaScheme(AreaScheme item)
    {
        return SafeElement(item, d => $"{d.Id}: {d.Name}");
    }

    public static string Area(Area item)
    {
        return SafeElement(item, d => $"{d.Id}: {d.Name}");
    }

    public static string Areas(List<Area> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"[");

        foreach (Area item in data)
        {
            printer.Append($"({Area(item)}), ");
        }

        printer.Append($"]");

        return printer.ToString();
    }

    public static string PropertyLine(PropertyLine item)
    {
        return SafeElement(item, d => $"{d.Id}: {d.Name}");
    }

    public static string Room(Room item)
    {
        return SafeElement(item, d => $"{d.Id}: {d.Name}");
    }

    public static string Rooms(List<Room> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"[");

        foreach (Room item in data)
        {
            printer.Append($"({Room(item)}), ");
        }

        printer.Append($"]");

        return printer.ToString();
    }

    public static string RoomsHashSet(HashSet<Room> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"[");

        foreach (Room item in data)
        {
            printer.Append($"({Room(item)}), ");
        }

        printer.Append($"]");

        return printer.ToString();
    }

    public static string ElementList(List<Element> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"(");

        foreach (Element item in data)
        {
            printer.Append($"({Element(item)}),");
        }

        printer.Append($")");

        return printer.ToString();
    }

    public static string Solids(List<Solid> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"(");

        foreach (Solid item in data)
        {
            printer.Append($"({Solid(item)}),");
        }

        printer.Append($")");

        return printer.ToString();
    }

    public static string Solid(Solid item)
    {
        return SafeReadGeometry(item, d => $"{d.Volume}", "Solid");
    }

    public static string StringSolidDictionary(Dictionary<string, Solid> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"[");
        printer.Append($"({data.Count}), ");
        printer.Append($"(");

        foreach (KeyValuePair<string, Solid> item in data)
        {
            printer.Append($"{item.Key}");
            printer.Append($"{Solid(item.Value)}");
        }

        printer.Append($")");
        printer.Append($"]");

        return printer.ToString();
    }

    public static string RoomSolidDictionary(Dictionary<Room, Solid> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"[");
        printer.Append($"({data.Count}), ");
        printer.Append($"(");

        foreach (KeyValuePair<Room, Solid> item in data)
        {
            printer.Append($"{Room(item.Key)}");
            printer.Append($": ");
            printer.Append($"{Solid(item.Value)}");
            printer.Append($", ");
        }

        printer.Append($")");
        printer.Append($"]");

        return printer.ToString();
    }

    public static string StringLineDictionary(Dictionary<string, Line> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"(");

        foreach (KeyValuePair<string, Line> item in data)
        {
            printer.Append($"{item.Key}, ");
            printer.Append($"{Line(item.Value)}");
        }

        printer.Append($")");

        return printer.ToString();
    }

    public static string ElementId(ElementId item)
    {
        // ElementIds are safe value-types/structs
        if (item == null) return "[null]";
        return $"{item}";
    }

    public static string Element(Element item)
    {
        return SafeElement(item, d => $"{d.Id}");
    }

    public static string ElementIdHashSet(HashSet<ElementId> data)
    {
        if (data == null) return "[null]";

        if (data.Count == 0) return "[empty]";

        return $"{data.Count}, [{string.Join(", ", data.Select(id => ElementId(id)))}]";
    }

    public static string ElementHashSet(HashSet<Element> items)
    {
        if (items == null) return "[null]";
        if (items.Count == 0) return "[empty]";

        var data = items.Select(a => Element(a));
        return $"[{string.Join(",", data)}]";
    }

    public static string ElementIdElementIdDictionary(Dictionary<ElementId, ElementId> items)
    {
        if (items == null) return "[null]";

        var data = new StringBuilder();

        data.AppendLine($"[");

        foreach (KeyValuePair<ElementId, ElementId> item in items)
        {
            data.Append($"({item.Key.ToString()}: {item.Value.ToString()}), ");
        }

        data.AppendLine($"");
        data.Append($"]");

        return data.ToString();
    }

    public static string FaceElementIdDictionary(Dictionary<Face, ElementId> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"(");

        foreach (KeyValuePair<Face, ElementId> item in data)
        {
            printer.Append($"(");
            printer.Append($"{Face(item.Key)}, ");
            printer.Append($"{ElementId(item.Value)}");
            printer.Append($"), ");
        }

        printer.Append($")");

        return printer.ToString();
    }

    public static string FaceStringDictionary(Dictionary<Face, string> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"(");

        foreach (KeyValuePair<Face, string> item in data)
        {
            printer.Append($"(");
            printer.Append($"{Face(item.Key)}, ");
            printer.Append($"{item.Value}");
            printer.Append($"), ");
        }

        printer.Append($")");

        return printer.ToString();
    }

    public static string StringReferenceDictionary(Dictionary<string, Reference> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"(");

        foreach (KeyValuePair<string, Reference> item in data)
        {
            printer.Append($"(");
            printer.Append($"{item.Key}, ");
            printer.Append($"{Reference(item.Value)}");
            printer.Append($"), ");
        }

        printer.Append($")");

        return printer.ToString();
    }

    public static string ReferenceStringDictionary(Dictionary<Reference, string> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"(");

        foreach (KeyValuePair<Reference, string> item in data)
        {
            printer.Append($"(");
            printer.Append($"{Reference(item.Key)}, ");
            printer.Append($"{item.Value}");
            printer.Append($"), ");
        }

        printer.Append($")");

        return printer.ToString();
    }

    public static string ElementIdFaceListDictionary(Dictionary<ElementId, List<Face>> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"[");

        foreach (KeyValuePair<ElementId, List<Face>> item in data)
        {
            printer.Append($"{item.Key}, ");
            printer.Append($"(");
            printer.Append($"{FaceList(item.Value)}");
            printer.Append($"), ");
        }

        printer.Append($"]");

        return printer.ToString();
    }

    public static string FaceList(List<Face> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"(");

        foreach (Face item in data)
        {
            printer.Append($"{Face(item)},");
        }

        printer.Append($")");

        return printer.ToString();
    }

    public static string Face(Face data)
    {
        return SafeReadGeometry(data, d => $"{d.Id}, {d.Area}", "Face");
    }

    public static string ElementIdFaceDictionary(Dictionary<ElementId, Face> items)
    {
        if (items == null) return "[null]";

        var data = new StringBuilder();

        foreach (KeyValuePair<ElementId, Face> item in items)
        {
            data.Append($"\n{ElementId(item.Key)} | {Face(item.Value)}");
        }

        return data.ToString();
    }

    public static string ElementIdReferenceDictionary(Dictionary<ElementId, Reference> items)
    {
        if (items == null) return "[null]";

        var data = new StringBuilder();

        foreach (KeyValuePair<ElementId, Reference> item in items)
        {
            data.Append($"\n{ElementId(item.Key)} | {Reference(item.Value)}");
        }

        return data.ToString();
    }

    public static string ElementIdSolidDictionary(Dictionary<ElementId, Solid> items)
    {
        if (items == null) return "[null]";

        var data = new StringBuilder();

        data.Append($"[");

        foreach (KeyValuePair<ElementId, Solid> item in items)
        {
            data.Append($"({ElementId(item.Key)}: {Solid(item.Value)}), ");
        }

        data.Append($"]");

        return data.ToString();
    }

    public static string ElementIdElementListDictionary(Dictionary<ElementId, List<Element>> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"[");

        foreach (KeyValuePair<ElementId, List<Element>> item in data)
        {
            printer.Append($"(");
            printer.Append($"{item.Key}, ");
            printer.Append($"(");

            foreach (Element element in item.Value)
            {
                printer.Append($"{Element(element)},");
            }

            printer.Append($")");
            printer.Append($"),");
        }

        printer.Append($"]");

        return printer.ToString();
    }

    public static string ElementIdElementIdHashSetDictionary(Dictionary<ElementId, HashSet<ElementId>> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"[");

        foreach (KeyValuePair<ElementId, HashSet<ElementId>> item in data)
        {
            printer.Append($"(");
            printer.Append($"{item.Key}, ");
            printer.Append($"(");

            foreach (ElementId element in item.Value)
            {
                printer.Append($"{element},");
            }

            printer.Append($")");
            printer.Append($"),");
        }

        printer.Append($"]");

        return printer.ToString();
    }

    public static string ElementIdElementIdListDictionary(Dictionary<ElementId, List<ElementId>> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"[");

        foreach (KeyValuePair<ElementId, List<ElementId>> item in data)
        {
            printer.Append($"(");
            printer.Append($"{item.Key}, ");
            printer.Append($"(");

            foreach (ElementId element in item.Value)
            {
                printer.Append($"{element},");
            }

            printer.Append($")");
            printer.Append($"),");
        }

        printer.Append($"]");

        return printer.ToString();
    }

    public static string Family(Family item)
    {
        return SafeElement(item, d => $"{d.Id}");
    }

    public static string FamilySymbol(FamilySymbol item)
    {
        return SafeElement(item, d => $"{d.Id}");
    }

    public static string FamilyInstance(FamilyInstance item)
    {
        return SafeElement(item, d => $"{d.Id}");
    }

    public static string FamilyInstanceList(List<FamilyInstance> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"(");

        foreach (FamilyInstance item in data)
        {
            printer.Append($"{FamilyInstance(item)},");
        }

        printer.Append($")");

        return printer.ToString();
    }

    public static string ElementIdFamilyInstanceListDictionary(Dictionary<ElementId, List<FamilyInstance>> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"[");

        foreach (KeyValuePair<ElementId, List<FamilyInstance>> item in data)
        {
            printer.Append($"(");
            printer.Append($"{item.Key}, ");
            printer.Append($"(");

            foreach (FamilyInstance element in item.Value)
            {
                printer.Append($"{FamilyInstance(element)},");
            }

            printer.Append($")");
            printer.Append($"),");
        }

        printer.Append($"]");

        return printer.ToString();
    }

    public static string ElementIdValueDictionary(Dictionary<ElementId, double> items)
    {
        if (items == null) return "[null]";

        var data = new StringBuilder();

        foreach (KeyValuePair<ElementId, double> item in items)
        {
            data.Append($"\n{item.Key.ToString()}: {item.Value.ToString()}");
        }

        return data.ToString();
    }

    public static string ElementIdValueListDictionary(Dictionary<ElementId, List<double>> items)
    {
        if (items == null) return "[null]";

        var data = new StringBuilder();

        foreach (KeyValuePair<ElementId, List<double>> item in items)
        {
            var data2 = new StringBuilder();

            foreach (double element in item.Value)
            {
                data2.Append($"{element},");
            }

            data.Append($"\n{item.Key.ToString()}: [{data2.ToString()}]");
        }

        return data.ToString();
    }

    public static string ElementIdErrorStringListDictionary(Dictionary<ElementId, List<string>> items)
    {
        if (items == null) return "[null]";

        var data = new StringBuilder();

        foreach (KeyValuePair<ElementId, List<string>> item in items)
        {
            var data2 = new StringBuilder();

            foreach (string element in item.Value)
            {
                data2.Append($"{element},");
            }

            data.Append($"\n{item.Key.ToString()}: [{data2.ToString()}]");
        }

        return data.ToString();
    }

    public static string ElementIdSolidListDictionary(Dictionary<ElementId, List<Solid>> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"[");

        foreach (KeyValuePair<ElementId, List<Solid>> item in data)
        {
            printer.Append($"(ID: ");
            printer.Append($"{ElementId(item.Key)}, ");
            printer.Append($"ITEMS: (");
            printer.Append($"{Solids(item.Value)}");
            printer.Append($")");
            printer.Append($"),");
        }

        printer.Append($"]");

        return printer.ToString();
    }

    public static string ElementIdSolidListTuple(List<(ElementId, List<Solid>)> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"[");

        foreach ((ElementId elementId, List<Solid> solidList) in data)
        {
            printer.Append($"(ID: ");
            printer.Append($"{ElementId(elementId)}, ");
            printer.Append($"ITEMS: (");
            printer.Append($"{Solids(solidList)}");
            printer.Append($")");
            printer.Append($"),");
        }

        printer.Append($"]");

        return printer.ToString();
    }

    public static string ElementIdXYZListDictionary(Dictionary<ElementId, List<XYZ>> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"[");

        foreach (KeyValuePair<ElementId, List<XYZ>> item in data)
        {
            printer.Append($"(");
            printer.Append($"{ElementId(item.Key)}, ");
            printer.Append($"(");
            printer.Append($"{XYZList(item.Value)}");
            printer.Append($"),");
            printer.Append($"),");
        }

        printer.Append($"]");

        return printer.ToString();
    }

    public static string XYZList(List<XYZ> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"(");

        foreach (XYZ item in data)
        {
            printer.Append($"{XYZ(item)}, ");
        }

        printer.Append($")");

        return printer.ToString();
    }

    public static string XYZ(XYZ data)
    {
        if (data == null) return "[null]";

        return $"{data.ToString()}";
    }

    public static string ElementIdXYZDictionary(Dictionary<ElementId, XYZ> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"[");

        foreach (KeyValuePair<ElementId, XYZ> item in data)
        {
            printer.Append($"({ElementId(item.Key)},{XYZ(item.Value)}),");
        }

        printer.Append($"]");

        return printer.ToString();
    }

    public static string ElementIdLineDictionary(Dictionary<ElementId, Line> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"[");

        foreach (KeyValuePair<ElementId, Line> item in data)
        {
            printer.Append($"({ElementId(item.Key)},{Line(item.Value)}),");
        }

        printer.Append($"]");

        return printer.ToString();
    }

    public static string ElementIdLineListDictionary(Dictionary<ElementId, List<Line>> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"[{data.Count}],");
        printer.Append($"[");

        foreach (KeyValuePair<ElementId, List<Line>> item in data)
        {
            printer.Append($"({item.Key}: [");

            foreach (Line item1 in item.Value)
            {
                printer.Append($"{Line(item1)}, ");
            }

            printer.Append($"]), ");
        }

        printer.Append($"]");

        return data.ToString();
    }

    public static string ElementDoubleTuple(List<(Element, double)> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"(");

        foreach ((Element, double) item in data)
        {
            printer.Append($"(");
            printer.Append($"{Element(item.Item1)}, ");
            printer.Append($"{item.Item2.ToString("F2", CultureInfo.InvariantCulture)}");
            printer.Append($"), ");
        }

        printer.Append($")");

        return printer.ToString();
    }

    public static string ElementDoubleDictionary(Dictionary<Element, double> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"(");

        foreach (KeyValuePair<Element, double> item in data)
        {
            printer.Append($"(");
            printer.Append($"{Element(item.Key)}, ");
            printer.Append($"{item.Value.ToString("F2", CultureInfo.InvariantCulture)}");
            printer.Append($"), ");
        }

        printer.Append($")");

        return printer.ToString();
    }

    public static string ElementIdDoubleDictionary(Dictionary<ElementId, double> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"(");

        foreach (KeyValuePair<ElementId, double> item in data)
        {
            printer.Append($"(");
            printer.Append($"{item.Key.ToString()}, ");
            printer.Append($"{item.Value.ToString("F2", CultureInfo.InvariantCulture)}");
            printer.Append($"), ");
        }

        printer.Append($")");

        return printer.ToString();
    }

    public static string ElementIdElementDictionary(Dictionary<ElementId, Element> items)
    {
        if (items == null) return "[null]";

        var data = new StringBuilder();

        foreach (KeyValuePair<ElementId, Element> item in items)
        {
            data.Append($"\n{ElementId(item.Key)} | {Element(item.Value)}");
        }

        return data.ToString();
    }

    public static string Long(long data)
    {
        return data.ToString();
    }

    public static string LongWallDictionary(Dictionary<long, Wall> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"(");

        foreach (KeyValuePair<long, Wall> item in data)
        {
            printer.Append($"(");
            printer.Append($"{item.Key}, ");
            printer.Append($"{Wall(item.Value)}");
            printer.Append($"), ");
        }

        printer.Append($")");

        return printer.ToString();
    }

    public static string LongFamilyInstanceDictionary(Dictionary<long, FamilyInstance> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"(");

        foreach (KeyValuePair<long, FamilyInstance> item in data)
        {
            printer.Append($"(");
            printer.Append($"{item.Key}, ");
            printer.Append($"{FamilyInstance(item.Value)}");
            printer.Append($"), ");
        }

        printer.Append($")");

        return printer.ToString();
    }

    public static string LongBoundarySegmentDictionary(Dictionary<long, BoundarySegment> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"(");

        foreach (KeyValuePair<long, BoundarySegment> item in data)
        {
            printer.Append($"(");
            printer.Append($"{item.Key}, ");
            printer.Append($"{BoundarySegment(item.Value)}");
            printer.Append($"), ");
        }

        printer.Append($")");

        return printer.ToString();
    }

    public static string LongBoundarySegmentListDictionary(Dictionary<long, List<BoundarySegment>> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"(");

        foreach (KeyValuePair<long, List<BoundarySegment>> item in data)
        {
            printer.Append($"(");
            printer.Append($"{item.Key}, ");
            printer.Append($"{BoundarySegmentList(item.Value)}");
            printer.Append($"), ");
        }

        printer.Append($")");

        return printer.ToString();
    }

    public static string LongIDtoDictionary(Dictionary<long, IDto> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"(");

        foreach (KeyValuePair<long, IDto> item in data)
        {
            printer.Append($"(");
            printer.Append($"{item.Key}, ");
            printer.Append($"{IDto(item.Value)}");
            printer.Append($"), ");
        }

        printer.Append($")");

        return printer.ToString();
    }

    public static string ElementIdElementsDictionary(Dictionary<ElementId, List<Element>> items)
    {
        if (items == null) return "[null]";

        var data = new StringBuilder();

        data.Append($"[{items.Count}], ");
        data.Append($"[");

        foreach (KeyValuePair<ElementId, List<Element>> item in items)
        {
            var data2 = new StringBuilder();

            foreach (Element element in item.Value)
            {
                data2.Append($"{Element(element)}, ");
            }

            data.Append($"({item.Key.ToString()}: [{item.Value.Count}], [{data2.ToString()}]), ");
        }

        data.Append($"]");

        return data.ToString();
    }

    public static string Plane(Plane data)
    {
        return SafeReadGeometry(data, d => $"{d.Origin.ToString()}", "Plane");
    }
    public static string Double(double data)
    {
        return $"{data.ToString("F2", CultureInfo.InvariantCulture)}";
    }
    public static string Integer(int data)
    {
        return $"{data.ToString()}";
    }
    public static string Boolean(bool data)
    {
        return $"{data.ToString()}";
    }
    public static string String(string data)
    {
        if (data == null) return "[null]";

        return $"{data}";
    }

    public static string Object(object data)
    {
        if (data == null) return "[null]";

        return $"{data}";
    }
    public static string Outline(Outline data)
    {
        return SafeReadGeometry(data, d => $"{d.MinimumPoint.ToString()} - {d.MaximumPoint.ToString()}", "Outline");
    }

    public static string ElementIdStringDictionary(Dictionary<ElementId, string> data)
    {
        if (data == null) return "[null]";

        var printer = new StringBuilder();

        printer.Append($"{data.Count}, ");
        printer.Append($"(");

        foreach (KeyValuePair<ElementId, string> item in data)
        {
            printer.Append($"(");
            printer.Append($"{ElementId(item.Key)}, ");
            printer.Append($"{item.Value}");
            printer.Append($"), ");
        }

        printer.Append($")");

        return printer.ToString();
    }
}