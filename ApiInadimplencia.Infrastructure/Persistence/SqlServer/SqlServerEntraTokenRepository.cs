using ApiInadimplencia.Application.Abstractions.Auth;
using ApiInadimplencia.Infrastructure.Auth;
using Dapper;

namespace ApiInadimplencia.Infrastructure.Persistence.SqlServer;

/// <summary>
/// SQL Server repository for encrypted Microsoft Entra ID refresh tokens.
/// </summary>
public sealed class SqlServerEntraTokenRepository(SqlServerConnectionFactory connectionFactory) : IEntraTokenRepository
{
    private const string TableName = "dbo.INAD_ENTRA_TOKENS";
    private readonly SqlServerConnectionFactory _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    private readonly SemaphoreSlim _ensureTableLock = new(1, 1);
    private bool _tableEnsured;

    /// <inheritdoc />
    public bool IsConfigured => _connectionFactory.IsConfigured;

    /// <inheritdoc />
    public async Task<StoredEntraToken?> FindByOwnerKeyAsync(string ownerKey, CancellationToken cancellationToken = default)
    {
        var normalizedOwnerKey = NormalizeOwnerKey(ownerKey);
        if (string.IsNullOrWhiteSpace(normalizedOwnerKey) || !IsConfigured)
        {
            return null;
        }

        await EnsureTableAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var command = new CommandDefinition(
            $"SELECT TOP 1 * FROM {TableName} WHERE FLUIG_USER_KEY = @ownerKey",
            new { ownerKey = normalizedOwnerKey },
            cancellationToken: cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<EntraTokenRow>(command).ConfigureAwait(false);
        return row is null ? null : Map(row);
    }

    /// <inheritdoc />
    public async Task<StoredEntraToken> UpsertAsync(
        string ownerKey,
        string? fluigUserName,
        string? fluigUserCode,
        string subject,
        EncryptedSecret encryptedRefreshToken,
        CancellationToken cancellationToken = default)
    {
        var normalizedOwnerKey = NormalizeOwnerKey(ownerKey);
        if (string.IsNullOrWhiteSpace(normalizedOwnerKey))
        {
            throw new AuthFailureException(400, "Usuario Fluig nao identificado para token Entra ID.", "FLUIG_USER_MISSING");
        }

        if (!IsConfigured)
        {
            throw new AuthFailureException(503, "SQL Server nao configurado para tokens Entra ID.", "SQL_NOT_CONFIGURED");
        }

        await EnsureTableAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var command = new CommandDefinition(
            $"""
            MERGE {TableName} AS target
            USING (SELECT @ownerKey AS FLUIG_USER_KEY) AS source
            ON target.FLUIG_USER_KEY = source.FLUIG_USER_KEY
            WHEN MATCHED THEN
              UPDATE SET
                FLUIG_USER_NAME = @fluigUserName,
                FLUIG_USER_CODE = @fluigUserCode,
                SUBJECT = @subject,
                REFRESH_TOKEN_CIPHER = @refreshTokenCipher,
                REFRESH_TOKEN_IV = @refreshTokenIv,
                REFRESH_TOKEN_TAG = @refreshTokenTag,
                UPDATED_AT = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
              INSERT (
                FLUIG_USER_KEY,
                FLUIG_USER_NAME,
                FLUIG_USER_CODE,
                SUBJECT,
                REFRESH_TOKEN_CIPHER,
                REFRESH_TOKEN_IV,
                REFRESH_TOKEN_TAG
              )
              VALUES (
                @ownerKey,
                @fluigUserName,
                @fluigUserCode,
                @subject,
                @refreshTokenCipher,
                @refreshTokenIv,
                @refreshTokenTag
              )
            OUTPUT inserted.*;
            """,
            new
            {
                ownerKey = normalizedOwnerKey,
                fluigUserName,
                fluigUserCode,
                subject,
                refreshTokenCipher = encryptedRefreshToken.CipherText,
                refreshTokenIv = encryptedRefreshToken.Iv,
                refreshTokenTag = encryptedRefreshToken.Tag,
            },
            cancellationToken: cancellationToken);
        var row = await connection.QuerySingleAsync<EntraTokenRow>(command).ConfigureAwait(false);
        return Map(row);
    }

    /// <inheritdoc />
    public async Task MarkLastLoginAsync(string ownerKey, CancellationToken cancellationToken = default)
    {
        var normalizedOwnerKey = NormalizeOwnerKey(ownerKey);
        if (string.IsNullOrWhiteSpace(normalizedOwnerKey) || !IsConfigured)
        {
            return;
        }

        await EnsureTableAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var command = new CommandDefinition(
            $"""
            UPDATE {TableName}
            SET LAST_LOGIN_AT = SYSUTCDATETIME(),
                UPDATED_AT = SYSUTCDATETIME()
            WHERE FLUIG_USER_KEY = @ownerKey
            """,
            new { ownerKey = normalizedOwnerKey },
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string ownerKey, CancellationToken cancellationToken = default)
    {
        var normalizedOwnerKey = NormalizeOwnerKey(ownerKey);
        if (string.IsNullOrWhiteSpace(normalizedOwnerKey) || !IsConfigured)
        {
            return;
        }

        await EnsureTableAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var command = new CommandDefinition(
            $"DELETE FROM {TableName} WHERE FLUIG_USER_KEY = @ownerKey",
            new { ownerKey = normalizedOwnerKey },
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command).ConfigureAwait(false);
    }

    private async Task EnsureTableAsync(CancellationToken cancellationToken)
    {
        if (_tableEnsured)
        {
            return;
        }

        await _ensureTableLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_tableEnsured)
            {
                return;
            }

            await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            var command = new CommandDefinition(
                $"""
                IF OBJECT_ID(N'{TableName}', N'U') IS NULL
                BEGIN
                  CREATE TABLE {TableName} (
                    ID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    FLUIG_USER_KEY NVARCHAR(255) NOT NULL,
                    FLUIG_USER_NAME NVARCHAR(255) NULL,
                    FLUIG_USER_CODE NVARCHAR(255) NULL,
                    SUBJECT NVARCHAR(255) NOT NULL,
                    REFRESH_TOKEN_CIPHER NVARCHAR(MAX) NOT NULL,
                    REFRESH_TOKEN_IV NVARCHAR(64) NOT NULL,
                    REFRESH_TOKEN_TAG NVARCHAR(64) NOT NULL,
                    CREATED_AT DATETIME2 NOT NULL CONSTRAINT DF_INAD_ENTRA_TOKENS_CREATED_AT DEFAULT SYSUTCDATETIME(),
                    UPDATED_AT DATETIME2 NOT NULL CONSTRAINT DF_INAD_ENTRA_TOKENS_UPDATED_AT DEFAULT SYSUTCDATETIME(),
                    LAST_LOGIN_AT DATETIME2 NULL
                  );
                END;

                IF NOT EXISTS (
                  SELECT 1
                  FROM sys.indexes
                  WHERE name = N'UX_INAD_ENTRA_TOKENS_FLUIG_USER_KEY'
                    AND object_id = OBJECT_ID(N'{TableName}')
                )
                BEGIN
                  CREATE UNIQUE INDEX UX_INAD_ENTRA_TOKENS_FLUIG_USER_KEY
                  ON {TableName} (FLUIG_USER_KEY);
                END;
                """,
                cancellationToken: cancellationToken);
            await connection.ExecuteAsync(command).ConfigureAwait(false);
            _tableEnsured = true;
        }
        finally
        {
            _ensureTableLock.Release();
        }
    }

    private static string NormalizeOwnerKey(string? value)
        => (value ?? string.Empty).Trim().ToLowerInvariant();

    private static StoredEntraToken Map(EntraTokenRow row)
        => new(
            row.ID,
            row.FLUIG_USER_KEY,
            row.FLUIG_USER_NAME,
            row.FLUIG_USER_CODE,
            row.SUBJECT,
            row.REFRESH_TOKEN_CIPHER,
            row.REFRESH_TOKEN_IV,
            row.REFRESH_TOKEN_TAG,
            row.CREATED_AT,
            row.UPDATED_AT,
            row.LAST_LOGIN_AT);

    private sealed record EntraTokenRow
    {
        public int ID { get; init; }
        public string FLUIG_USER_KEY { get; init; } = string.Empty;
        public string? FLUIG_USER_NAME { get; init; }
        public string? FLUIG_USER_CODE { get; init; }
        public string SUBJECT { get; init; } = string.Empty;
        public string REFRESH_TOKEN_CIPHER { get; init; } = string.Empty;
        public string REFRESH_TOKEN_IV { get; init; } = string.Empty;
        public string REFRESH_TOKEN_TAG { get; init; } = string.Empty;
        public DateTime CREATED_AT { get; init; }
        public DateTime UPDATED_AT { get; init; }
        public DateTime? LAST_LOGIN_AT { get; init; }
    }
}
