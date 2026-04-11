using System.Text;
using Autodesk.Revit.DB;

namespace Eobim.RevitApi.Framework;

public class Messenger
{
    private readonly StringBuilder _stringBuilder = new();
    public Messenger(Document doc, string commandName)
    {
        _stringBuilder.AppendLine($"Revit Document Title: {doc.Title}");
        _stringBuilder.AppendLine($"Command: {commandName}");

    }

    public void Action(string actionName, string tabs = "")
    {
        _stringBuilder.AppendLine($"{tabs}{actionName}");
    }

    public void Exception(System.Exception ex)
    {
#if DEBUG
        _stringBuilder.AppendLine(ex.Message);
        _stringBuilder.AppendLine(ex.StackTrace);
#else
        _stringBuilder.AppendLine(ex.Message);
        _stringBuilder.AppendLine(ex.StackTrace);
#endif
    }

    public string Dump() => _stringBuilder.ToString();
}