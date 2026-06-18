using System.Collections;
using System.Reflection;
using ApiInadimplencia.Infrastructure.Persistence.SqlServer;
using FluentAssertions;

namespace ApiInadimplencia.Infrastructure.Tests.Persistence.SqlServer;

public sealed class LegacySqlExecutorDefinitionTests
{
    [Fact]
    public void ResponsaveisUpsert_UsesRequiredSetOptionsAndTransaction()
    {
        var commandsField = typeof(LegacySqlExecutor).GetField(
            "Commands",
            BindingFlags.Static | BindingFlags.NonPublic);

        commandsField.Should().NotBeNull();
        var commands = commandsField!.GetValue(null) as IDictionary;
        commands.Should().NotBeNull();

        var definition = commands!["Responsaveis.Upsert"];
        definition.Should().NotBeNull();

        var sqlProperty = definition!.GetType().GetProperty("Sql");
        var sql = sqlProperty!.GetValue(definition) as string;

        sql.Should().Contain("SET QUOTED_IDENTIFIER ON");
        sql.Should().Contain("SET ARITHABORT ON");
        sql.Should().Contain("SET NUMERIC_ROUNDABORT OFF");
        sql.Should().Contain("SET XACT_ABORT ON");
        sql.Should().Contain("BEGIN TRANSACTION");
        sql.Should().Contain("COMMIT TRANSACTION");
        sql.Should().Contain("ROLLBACK TRANSACTION");
    }
}
