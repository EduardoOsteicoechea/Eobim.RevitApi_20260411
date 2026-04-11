namespace Eobim.RevitApi.Framework;

public struct BooleanResult
{
    public bool IsError { get; set; } = false;
    public string? ErrorMessage { get; set; }
    public bool? Value { get; set; }
    private BooleanResult(bool? value, string? error)
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
    public static BooleanResult Success(bool value)
    {
        return new BooleanResult(value, null);
    }
    public static BooleanResult Failure(string message)
    {
        return new BooleanResult(default, message);
    }
}