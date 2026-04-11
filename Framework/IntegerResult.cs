namespace Eobim.RevitApi.Framework;

public struct IntegerResult
{
    public bool IsError { get; set; }
    public string? ErrorMessage { get; set; }
    public int? Value { get; set; }
    private IntegerResult(int? value, string? error)
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
    public static IntegerResult Success(int value)
    {
        return new IntegerResult(value, null);
    }
    public static IntegerResult Failure(string message)
    {
        return new IntegerResult(default, message);
    }
}