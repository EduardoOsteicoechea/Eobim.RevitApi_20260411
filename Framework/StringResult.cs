namespace Eobim.RevitApi.Framework;

public struct StringResult
{
    public bool IsError { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Value { get; set; }
    private StringResult(string? value, string? error)
    {
        if (error is not null)
        {
            ErrorMessage = error;
            IsError = true;
        }
        else
        {
            if (value is null)
            {
                IsError = true;
                throw new ArgumentNullException(nameof(value));
            }
            else
            {
                IsError = false;
                Value = value;
            }
        }
    }
    public static StringResult Success(string value)
    {
        return new StringResult(value, null);
    }
    public static StringResult Failure(string message)
    {
        return new StringResult(default, message);
    }
}