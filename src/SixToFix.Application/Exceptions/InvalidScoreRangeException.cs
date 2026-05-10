namespace SixToFix.Application.Exceptions;

public sealed class InvalidScoreRangeException : Exception
{
    public decimal Value { get; }
    public decimal Min { get; }
    public decimal Max { get; }

    public InvalidScoreRangeException(decimal value, decimal min, decimal max)
        : base($"Score {value} is outside the allowed range [{min}, {max}].")
    {
        Value = value;
        Min = min;
        Max = max;
    }
}
