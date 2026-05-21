namespace SixToFix.Application.Services;

public interface IDeploymentInfoService
{
    DeploymentInfo GetDeploymentInfo();
}

/// <summary>
/// Non-sensitive deployment metadata surfaced to the UI.
/// All fields are optional — callers must handle null gracefully.
/// </summary>
public sealed record DeploymentInfo(
    DateTimeOffset? BuildTimestamp,
    DateTimeOffset? DeployedAt,
    string? CommitSha);
