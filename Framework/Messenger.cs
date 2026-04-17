using System.Text;
using Autodesk.Revit.DB;

namespace Eobim.RevitApi.Framework;

public class Messenger
{
    private readonly StringBuilder _stringBuilder = new();

	public void Collect(string value)
	{
		_stringBuilder.AppendLine($"{value}");
	}

    public void Exception(System.Exception ex, string actionName = "")
    {
#if DEBUG
        _stringBuilder.AppendLine(ex.Message);
        _stringBuilder.AppendLine(ex.StackTrace);
#else
        _stringBuilder.AppendLine($"Message: {ex.Message}");
        _stringBuilder.AppendLine("StackTrace:");
        _stringBuilder.AppendLine(ex.StackTrace);
#endif
    }

    public string Dump() => _stringBuilder.ToString();
}