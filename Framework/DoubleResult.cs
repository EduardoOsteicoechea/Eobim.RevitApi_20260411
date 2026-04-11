namespace Eobim.RevitApi.Framework;

public struct DoubleResult
{
    public bool IsError { get; set; }
    public string? ErrorMessage { get; set; }
    public double Value { get; set; }
    private DoubleResult(double value, string? error)
    {
        if (error is not null)
        {
            ErrorMessage = error;
            IsError = true;
        }
        else
        {
            IsError = false;
            Value = value;
        }
    }
    public static DoubleResult Success(double value)
    {
        return new DoubleResult(value, null);
    }
    public static DoubleResult Failure(string message)
    {
        return new DoubleResult(default, message);
    }
}