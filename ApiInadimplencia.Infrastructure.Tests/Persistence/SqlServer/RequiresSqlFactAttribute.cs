using Xunit;

namespace ApiInadimplencia.Infrastructure.Tests.Persistence.SqlServer;

internal sealed class RequiresSqlFactAttribute : FactAttribute
{
    public RequiresSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING")))
        {
            Skip = "Requires TEST_CONNECTION_STRING for real SQL Server integration tests.";
        }
    }
}
