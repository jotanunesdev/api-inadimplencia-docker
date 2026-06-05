using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Domain.SerasaPefin;
using Microsoft.Data.SqlClient;
using System.Data;

namespace ApiInadimplencia.Infrastructure.Persistence.SqlServer;

/// <summary>
/// ADO.NET implementation of <see cref="ISerasaPefinBaixaRepository"/> targeting
/// <c>dbo.SERASA_PEFIN_BAIXAS</c> and (for webhooks) <c>dbo.SERASA_PEFIN_WEBHOOKS</c>.
/// All multi-row writes use SERIALIZABLE transactions; the filtered unique index
/// <c>UX_SERASA_PEFIN_BAIXAS_ATIVA</c> enforces single active baixa per parcela.
/// </summary>
public sealed class SerasaPefinBaixaRepository(SqlServerConnectionFactory connectionFactory)
    : ISerasaPefinBaixaRepository
{
    private const int SqlErrorDuplicateKey = 2601;
    private const int SqlErrorUniqueConstraint = 2627;

    private const string SelectColumns = """
        ID, ID_SOLICITACAO_NEGATIVACAO, NUM_VENDA_FK, NUMERO_PARCELA, CONTRACT_NUMBER,
        DOCUMENTO_DEVEDOR, DOCUMENTO_CREDOR, MOTIVO, STATUS, SOLICITANTE_USERNAME,
        APROVADOR_USERNAME, DT_APROVACAO, JUSTIFICATIVA, TRANSACTION_ID, WEBHOOK_PAYLOAD,
        ERROR_MESSAGE, ERROR_STATUS_CODE, TENTATIVAS, DT_CRIACAO, DT_ATUALIZACAO
        """;

    private readonly SqlServerConnectionFactory _connectionFactory = connectionFactory
        ?? throw new ArgumentNullException(nameof(connectionFactory));

    /// <inheritdoc />
    public async Task<Guid> AddAsync(SerasaPefinBaixaSolicitacao baixa, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baixa);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken).ConfigureAwait(false);

        try
        {
            await InsertBaixaAsync(connection, transaction, baixa, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return baixa.Id;
        }
        catch (SqlException ex) when (ex.Number is SqlErrorDuplicateKey or SqlErrorUniqueConstraint)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw new SerasaPefinBaixaDuplicateActiveException(
                "Active Serasa PEFIN baixa already exists for the same (NUM_VENDA, CONTRACT_NUMBER, NUMERO_PARCELA) combination.",
                ex);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task AddManyAsync(IReadOnlyCollection<SerasaPefinBaixaSolicitacao> baixas, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baixas);

        if (baixas.Count == 0)
        {
            return;
        }

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken).ConfigureAwait(false);

        try
        {
            foreach (var baixa in baixas)
            {
                ArgumentNullException.ThrowIfNull(baixa);
                await InsertBaixaAsync(connection, transaction, baixa, cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqlException ex) when (ex.Number is SqlErrorDuplicateKey or SqlErrorUniqueConstraint)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw new SerasaPefinBaixaDuplicateActiveException(
                "Active Serasa PEFIN baixa already exists for the same (NUM_VENDA, CONTRACT_NUMBER, NUMERO_PARCELA) combination.",
                ex);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UpdateAsync(SerasaPefinBaixaSolicitacao baixa, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baixa);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = BuildUpdateCommand(connection, null, baixa);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<SerasaPefinBaixaSolicitacao?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var sql = $"SELECT {SelectColumns} FROM dbo.SERASA_PEFIN_BAIXAS WHERE ID = @Id;";
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? MapReader(reader) : null;
    }

    /// <inheritdoc />
    public async Task<SerasaPefinBaixaSolicitacao?> GetByTransactionIdAsync(string transactionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(transactionId))
        {
            return null;
        }

        var sql = $"SELECT {SelectColumns} FROM dbo.SERASA_PEFIN_BAIXAS WHERE TRANSACTION_ID = @TransactionId;";
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@TransactionId", SqlDbType.VarChar, 64).Value = transactionId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? MapReader(reader) : null;
    }

    /// <inheritdoc />
    public async Task<bool> ExistsActiveAsync(
        int numVendaFk,
        string contractNumber,
        int? numeroParcela,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP 1 1
            FROM dbo.SERASA_PEFIN_BAIXAS
            WHERE NUM_VENDA_FK = @NumVendaFk
              AND CONTRACT_NUMBER = @ContractNumber
              AND ((@NumeroParcela IS NULL AND NUMERO_PARCELA IS NULL)
                OR NUMERO_PARCELA = @NumeroParcela)
              AND STATUS IN (
                  'AGUARDANDO_APROVACAO',
                  'APROVADA',
                  'PENDENTE_ENVIO',
                  'BAIXA_ENVIADA',
                  'BAIXA_AGUARDANDO_RETORNO'
              );
            """;

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@NumVendaFk", SqlDbType.Int).Value = numVendaFk;
        command.Parameters.Add("@ContractNumber", SqlDbType.VarChar, 20).Value = contractNumber;
        command.Parameters.Add("@NumeroParcela", SqlDbType.Int).Value = (object?)numeroParcela ?? DBNull.Value;

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is not null && result != DBNull.Value;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SerasaPefinBaixaSolicitacao>> ListByStatusAsync(
        SerasaPefinBaixaStatus? status,
        int? numVenda,
        string? solicitanteUsername,
        int take,
        int skip,
        CancellationToken cancellationToken)
    {
        var whereConditions = new List<string>();
        var parameters = new List<SqlParameter>();

        if (status.HasValue)
        {
            whereConditions.Add("STATUS = @Status");
            parameters.Add(new SqlParameter("@Status", SqlDbType.VarChar, 40) { Value = status.Value.ToDbValue() });
        }

        if (numVenda.HasValue)
        {
            whereConditions.Add("NUM_VENDA_FK = @NumVenda");
            parameters.Add(new SqlParameter("@NumVenda", SqlDbType.Int) { Value = numVenda.Value });
        }

        if (!string.IsNullOrWhiteSpace(solicitanteUsername))
        {
            whereConditions.Add("SOLICITANTE_USERNAME = @SolicitanteUsername");
            parameters.Add(new SqlParameter("@SolicitanteUsername", SqlDbType.VarChar, 100) { Value = solicitanteUsername });
        }

        var whereClause = whereConditions.Count > 0
            ? $"WHERE {string.Join(" AND ", whereConditions)}"
            : string.Empty;

        var sql = $"""
            SELECT {SelectColumns}
            FROM dbo.SERASA_PEFIN_BAIXAS
            {whereClause}
            ORDER BY DT_CRIACAO DESC
            OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;
            """;

        parameters.Add(new SqlParameter("@Skip", SqlDbType.Int) { Value = skip });
        parameters.Add(new SqlParameter("@Take", SqlDbType.Int) { Value = take });

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddRange(parameters.ToArray());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var results = new List<SerasaPefinBaixaSolicitacao>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(MapReader(reader));
        }

        return results;
    }

    /// <inheritdoc />
    public async Task ApplyWebhookTransactionalAsync(
        SerasaPefinBaixaSolicitacao baixa,
        SerasaPefinWebhookRecord webhook,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baixa);
        ArgumentNullException.ThrowIfNull(webhook);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken).ConfigureAwait(false);

        try
        {
            await using (var updateCommand = BuildUpdateCommand(connection, transaction, baixa))
            {
                await updateCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            const string webhookSql = """
                INSERT INTO dbo.SERASA_PEFIN_WEBHOOKS
                    (ID, EVENT_TYPE, TRANSACTION_ID, PAYLOAD, MATCHED_SOLICITACAO_ID, PROCESSADO, MENSAGEM_ERRO, DT_RECEBIMENTO)
                VALUES
                    (@Id, @EventType, @TransactionId, @Payload, @MatchedSolicitacaoId, @Processado, @MensagemErro, @DtRecebimento);
                """;

            await using var webhookCommand = new SqlCommand(webhookSql, connection, transaction);
            webhookCommand.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = webhook.Id;
            webhookCommand.Parameters.Add("@EventType", SqlDbType.VarChar, 64).Value = webhook.EventType;
            webhookCommand.Parameters.Add("@TransactionId", SqlDbType.VarChar, 64).Value = (object?)webhook.TransactionId ?? DBNull.Value;
            webhookCommand.Parameters.Add("@Payload", SqlDbType.NVarChar, -1).Value = webhook.Payload;
            webhookCommand.Parameters.Add("@MatchedSolicitacaoId", SqlDbType.UniqueIdentifier).Value = (object?)webhook.MatchedSolicitacaoId ?? DBNull.Value;
            webhookCommand.Parameters.Add("@Processado", SqlDbType.Bit).Value = webhook.Processado;
            webhookCommand.Parameters.Add("@MensagemErro", SqlDbType.NVarChar, 1000).Value = (object?)webhook.MensagemErro ?? DBNull.Value;
            webhookCommand.Parameters.Add("@DtRecebimento", SqlDbType.DateTime2, 0).Value = webhook.DtRecebimento;
            await webhookCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static SqlCommand BuildUpdateCommand(
        SqlConnection connection,
        SqlTransaction? transaction,
        SerasaPefinBaixaSolicitacao b)
    {
        const string sql = """
            UPDATE dbo.SERASA_PEFIN_BAIXAS SET
                STATUS = @Status,
                APROVADOR_USERNAME = @AprovadorUsername,
                DT_APROVACAO = @DtAprovacao,
                JUSTIFICATIVA = @Justificativa,
                TRANSACTION_ID = @TransactionId,
                WEBHOOK_PAYLOAD = @WebhookPayload,
                ERROR_MESSAGE = @ErrorMessage,
                ERROR_STATUS_CODE = @ErrorStatusCode,
                TENTATIVAS = @Tentativas,
                DT_ATUALIZACAO = @DtAtualizacao
            WHERE ID = @Id;
            """;

        var command = transaction is null
            ? new SqlCommand(sql, connection)
            : new SqlCommand(sql, connection, transaction);

        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = b.Id;
        command.Parameters.Add("@Status", SqlDbType.VarChar, 40).Value = b.Status.ToDbValue();
        command.Parameters.Add("@AprovadorUsername", SqlDbType.VarChar, 100).Value = (object?)b.AprovadorUsername ?? DBNull.Value;
        command.Parameters.Add("@DtAprovacao", SqlDbType.DateTime2, 0).Value = (object?)b.DtAprovacao ?? DBNull.Value;
        command.Parameters.Add("@Justificativa", SqlDbType.NVarChar, 500).Value = (object?)b.Justificativa ?? DBNull.Value;
        command.Parameters.Add("@TransactionId", SqlDbType.VarChar, 64).Value = (object?)b.TransactionId ?? DBNull.Value;
        command.Parameters.Add("@WebhookPayload", SqlDbType.NVarChar, -1).Value = (object?)b.WebhookPayload ?? DBNull.Value;
        command.Parameters.Add("@ErrorMessage", SqlDbType.NVarChar, 1000).Value = (object?)b.ErrorMessage ?? DBNull.Value;
        command.Parameters.Add("@ErrorStatusCode", SqlDbType.Int).Value = (object?)b.ErrorStatusCode ?? DBNull.Value;
        command.Parameters.Add("@Tentativas", SqlDbType.TinyInt).Value = b.Tentativas;
        command.Parameters.Add("@DtAtualizacao", SqlDbType.DateTime2, 0).Value = b.DtAtualizacao;
        return command;
    }

    private static async Task InsertBaixaAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        SerasaPefinBaixaSolicitacao b,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.SERASA_PEFIN_BAIXAS
                (ID, ID_SOLICITACAO_NEGATIVACAO, NUM_VENDA_FK, NUMERO_PARCELA, CONTRACT_NUMBER,
                 DOCUMENTO_DEVEDOR, DOCUMENTO_CREDOR, MOTIVO, STATUS, SOLICITANTE_USERNAME,
                 APROVADOR_USERNAME, DT_APROVACAO, JUSTIFICATIVA, TRANSACTION_ID, WEBHOOK_PAYLOAD,
                 ERROR_MESSAGE, ERROR_STATUS_CODE, TENTATIVAS, DT_CRIACAO, DT_ATUALIZACAO)
            VALUES
                (@Id, @IdSolicitacaoNegativacao, @NumVendaFk, @NumeroParcela, @ContractNumber,
                 @DocumentoDevedor, @DocumentoCredor, @Motivo, @Status, @SolicitanteUsername,
                 @AprovadorUsername, @DtAprovacao, @Justificativa, @TransactionId, @WebhookPayload,
                 @ErrorMessage, @ErrorStatusCode, @Tentativas, @DtCriacao, @DtAtualizacao);
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = b.Id;
        command.Parameters.Add("@IdSolicitacaoNegativacao", SqlDbType.UniqueIdentifier).Value = b.IdSolicitacaoNegativacao;
        command.Parameters.Add("@NumVendaFk", SqlDbType.Int).Value = b.NumVendaFk;
        command.Parameters.Add("@NumeroParcela", SqlDbType.Int).Value = (object?)b.NumeroParcela ?? DBNull.Value;
        command.Parameters.Add("@ContractNumber", SqlDbType.VarChar, 20).Value = b.ContractNumber;
        command.Parameters.Add("@DocumentoDevedor", SqlDbType.VarChar, 20).Value = b.DocumentoDevedor;
        command.Parameters.Add("@DocumentoCredor", SqlDbType.VarChar, 20).Value = b.DocumentoCredor;
        command.Parameters.Add("@Motivo", SqlDbType.TinyInt).Value = b.Motivo.Codigo;
        command.Parameters.Add("@Status", SqlDbType.VarChar, 40).Value = b.Status.ToDbValue();
        command.Parameters.Add("@SolicitanteUsername", SqlDbType.VarChar, 100).Value = b.SolicitanteUsername;
        command.Parameters.Add("@AprovadorUsername", SqlDbType.VarChar, 100).Value = (object?)b.AprovadorUsername ?? DBNull.Value;
        command.Parameters.Add("@DtAprovacao", SqlDbType.DateTime2, 0).Value = (object?)b.DtAprovacao ?? DBNull.Value;
        command.Parameters.Add("@Justificativa", SqlDbType.NVarChar, 500).Value = (object?)b.Justificativa ?? DBNull.Value;
        command.Parameters.Add("@TransactionId", SqlDbType.VarChar, 64).Value = (object?)b.TransactionId ?? DBNull.Value;
        command.Parameters.Add("@WebhookPayload", SqlDbType.NVarChar, -1).Value = (object?)b.WebhookPayload ?? DBNull.Value;
        command.Parameters.Add("@ErrorMessage", SqlDbType.NVarChar, 1000).Value = (object?)b.ErrorMessage ?? DBNull.Value;
        command.Parameters.Add("@ErrorStatusCode", SqlDbType.Int).Value = (object?)b.ErrorStatusCode ?? DBNull.Value;
        command.Parameters.Add("@Tentativas", SqlDbType.TinyInt).Value = b.Tentativas;
        command.Parameters.Add("@DtCriacao", SqlDbType.DateTime2, 0).Value = b.DtCriacao;
        command.Parameters.Add("@DtAtualizacao", SqlDbType.DateTime2, 0).Value = b.DtAtualizacao;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static SerasaPefinBaixaSolicitacao MapReader(SqlDataReader reader)
    {
        return SerasaPefinBaixaSolicitacao.Hydrate(
            id: reader.GetGuid(0),
            idSolicitacaoNegativacao: reader.GetGuid(1),
            numVendaFk: reader.GetInt32(2),
            numeroParcela: reader.IsDBNull(3) ? null : reader.GetInt32(3),
            contractNumber: reader.GetString(4),
            documentoDevedor: reader.GetString(5),
            documentoCredor: reader.GetString(6),
            motivo: SerasaPefinBaixaMotivo.From(reader.GetByte(7)),
            status: SerasaPefinBaixaStatusExtensions.ParseBaixaStatus(reader.GetString(8)),
            solicitanteUsername: reader.GetString(9),
            aprovadorUsername: reader.IsDBNull(10) ? null : reader.GetString(10),
            dtAprovacao: reader.IsDBNull(11) ? null : reader.GetDateTime(11),
            justificativa: reader.IsDBNull(12) ? null : reader.GetString(12),
            transactionId: reader.IsDBNull(13) ? null : reader.GetString(13),
            webhookPayload: reader.IsDBNull(14) ? null : reader.GetString(14),
            errorMessage: reader.IsDBNull(15) ? null : reader.GetString(15),
            errorStatusCode: reader.IsDBNull(16) ? null : reader.GetInt32(16),
            tentativas: reader.GetByte(17),
            dtCriacao: reader.GetDateTime(18),
            dtAtualizacao: reader.GetDateTime(19));
    }
}
