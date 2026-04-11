using System;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace Eobim.RevitApi.Core;

public static class RevitFamily
{
	// ==========================================
	// LOAD FAMILY
	// ==========================================
	public static void Load(Document doc, string filePath)
	{
		using (Transaction t = new Transaction(doc, "Load Family"))
		{
			t.Start();
			LoadTransactionless(doc, filePath);
			t.Commit();
		}
	}

	/// <summary>
	/// Loads a family into the document. Requires an active open transaction.
	/// </summary>
	public static void LoadTransactionless(Document doc, string filePath)
	{
		if (!File.Exists(filePath))
		{
			throw new ArgumentException("Invalid Path");
		}

		doc.LoadFamily(filePath, out Family _);
	}

	// ==========================================
	// GET SYMBOL (Read-Only, no transaction needed)
	// ==========================================
	/// <summary>
	/// Retrieves a FamilySymbol from the document by its Family Name and Type Name.
	/// This only reads data, so it never requires a transaction.
	/// </summary>
	public static FamilySymbol GetSymbol(Document doc, string familyName, string typeName)
	{
		FilteredElementCollector collector = new FilteredElementCollector(doc)
			.OfClass(typeof(FamilySymbol));

		return collector
			.Cast<FamilySymbol>()
			.FirstOrDefault(sym => sym.FamilyName == familyName && sym.Name == typeName);
	}

	// ==========================================
	// PLACE LINE-BASED FAMILY
	// ==========================================
	public static FamilyInstance PlaceLineBased(Document doc, FamilySymbol symbol, Level level, XYZ startPoint, XYZ endPoint)
	{
		var result = default(FamilyInstance);

		if (symbol == null || level == null || startPoint.IsAlmostEqualTo(endPoint)) return result;

		using (Transaction t = new Transaction(doc, "Place Line-Based Family"))
		{
			t.Start();
			result = PlaceLineBasedTransactionless(doc, symbol, level, startPoint, endPoint);
			t.Commit();
		}

		return result;
	}

	/// <summary>
	/// Places a line-based family instance. Requires an active open transaction.
	/// </summary>
	public static FamilyInstance PlaceLineBasedTransactionless(Document doc, FamilySymbol symbol, Level level, XYZ startPoint, XYZ endPoint)
	{
		if (symbol == null || level == null || startPoint.IsAlmostEqualTo(endPoint)) return null;

		// Ensure the symbol is active. Because we are inside a transactionless method,
		// we assume the caller has already opened a transaction for us to do this.
		if (!symbol.IsActive)
		{
			symbol.Activate();
			doc.Regenerate(); // Ensure the document recognizes the activation
		}

		Line locationLine = Line.CreateBound(startPoint, endPoint);

		return doc.Create.NewFamilyInstance(
			locationLine,
			symbol,
			level,
			StructuralType.NonStructural);
	}

	// ==========================================
	// SET PARAMETER
	// ==========================================
	public static bool SetSharedParameterValueByParameterName(Element element, string parameterName, double value)
	{
		if (element == null) return false;

		bool result = false;
		using (Transaction t = new Transaction(element.Document, "Set Parameter Value"))
		{
			t.Start();
			result = SetSharedParameterValueByParameterNameTransactionless(element, parameterName, value);
			t.Commit();
		}

		return result;
	}

	/// <summary>
	/// Sets an instance or type parameter value by its name. Requires an active open transaction.
	/// </summary>
	public static bool SetSharedParameterValueByParameterNameTransactionless(Element element, string parameterName, double value)
	{
		if (element == null) return false;

		// 1. Try to find an Instance parameter
		Parameter param = element.LookupParameter(parameterName);

		if (param != null && !param.IsReadOnly)
		{
			return param.Set(value);
		}

		// 2. Fallback: Try to find a Type parameter if it wasn't an Instance parameter
		ElementId typeId = element.GetTypeId();
		if (typeId != ElementId.InvalidElementId)
		{
			Element elementType = element.Document.GetElement(typeId);
			Parameter typeParam = elementType.LookupParameter(parameterName);

			if (typeParam != null && !typeParam.IsReadOnly)
			{
				return typeParam.Set(value);
			}
		}

		// Return false if the parameter doesn't exist or is locked by a formula
		return false;
	}
}