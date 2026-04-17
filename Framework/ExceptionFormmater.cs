namespace Eobim.RevitApi.Framework;

public static class ExceptionFormmater
{
    public static void AsString(Exception a, List<string> _telemetry) 
    {
        _telemetry.Add(a.Message);
        _telemetry.Add(a.StackTrace);
    }
}