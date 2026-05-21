using SixToFix.Application.Services;

namespace SixToFix.Infrastructure.Services;

/// <summary>
/// Reads deployment metadata from IConfiguration (sourced from App Settings or environment
/// variables injected by the CI/CD pipeline). All values are optional — missing keys fall
/// back to null so the UI can degrade gracefully.
/// </summary>
public sealed class DeploymentInfoService : IDeploymentInfoService
{
    private readonly DeploymentInfo _info;

    public DeploymentInfoService(IConfiguration configuration)
    {
        var rawBuild = configuration["Deploy:BuildTimestamp"];
        DateTimeOffset? buildTimestamp = DateTimeOffset.TryParse(rawBuild, out var parsedBuild) ? parsedBuild : null;

        var rawDeploy = configuration["Deploy:Timestamp"];
        DateTimeOffset? deployedAt = DateTimeOffset.TryParse(rawDeploy, out var parsedDeploy) ? parsedDeploy : null;

        var sha = configuration["Deploy:CommitSha"];
        // Trim to short SHA (7 chars) and reject placeholder/empty values
        var commitSha = string.IsNullOrWhiteSpace(sha) ? null : sha.Trim()[..Math.Min(7, sha.Trim().Length)];

        _info = new DeploymentInfo(buildTimestamp, deployedAt, commitSha);
    }

    public DeploymentInfo GetDeploymentInfo() => _info;
}
