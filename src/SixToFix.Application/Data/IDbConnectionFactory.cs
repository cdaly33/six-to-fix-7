using System.Data;

namespace SixToFix.Application.Data;

/// <summary>
/// pgBouncer-aware connection factory. Always uses port 6432 for transaction pooling.
/// </summary>
public interface IDbConnectionFactory
{
    Task<IDbConnection> CreateConnectionAsync(CancellationToken ct = default);
}
