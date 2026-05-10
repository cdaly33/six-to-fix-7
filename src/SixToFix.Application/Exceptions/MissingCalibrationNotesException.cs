namespace SixToFix.Application.Exceptions;

public sealed class MissingCalibrationNotesException : Exception
{
    public MissingCalibrationNotesException()
        : base("Calibration notes are required for score adjustments.")
    {
    }
}
