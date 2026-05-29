using Microsoft.Data.SqlClient;

namespace ApiInadimplencia.Infrastructure.Tests.Persistence.SqlServer;

internal static class SqlIntegrationTestGuard
{
    public static string RequireAvailableConnectionString(string testSuiteName)
    {
        var connectionString = Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"{testSuiteName} requires TEST_CONNECTION_STRING.");
        }

        var builder = new SqlConnectionStringBuilder(connectionString);
        builder.ConnectTimeout = builder.ConnectTimeout > 0 ? Math.Min(builder.ConnectTimeout, 5) : 2;
        return builder.ConnectionString;
    }
}
