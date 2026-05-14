using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Domain.SerasaPefin;
using Microsoft.Data.SqlClient;
using System.Data;

namespace ApiInadimplencia.Infrastructure.Persistence.SqlServer;

/// <summary>
/// ADO.NET-based implementation of <see cref="ISerasaPefinRepository"/> targeting
/// <c>dbo.SERASA_PEFIN_SOLICITACOES</c> and <c>dbo.SERASA_PEFIN_WEBHOOKS</c>.
/// All write operations use SERIALIZABLE transactions, matching the Node reference model.
/// </summary>
public sealed class SerasaPefinRepository(SqlServerConnectionFactory connectionFactory)
    : ISerasaPefinRepository
{
    private const int SqlErrorDuplicateKey = 2601;
    private const int SqlErrorUniqueConstraint = 2627;

    private const string SelectColumns = """
        ID, NUM_VENDA_FK, TIPO_REGISTRO, ID_SOLICITACAO_PRINCIPAL, ID_ASSOCIADO, TIPO_ASSOCIACAO,
        DOCUMENTO_DEVEDOR, DOCUMENTO_GARANTIDOR, DOCUMENTO_CREDOR, CONTRACT_NUMBER, CATEGORY_ID,
        AREA_INFORMANTE, VALOR, DATA_VENCIMENTO, STATUS, TRANSACTION_ID, CADUS_KEY, CADUS_SERIE,
        PAYLOAD_AUDITORIA, WEBHOOK_PAYLOAD, ERROR_MESSAGE, ERROR_STATUS_CODE, OPERADOR,
        DT_CRIACAO, DT_ATUALIZACAO
        """;

    private readonly SqlServerConnectionFactory _connectionFactory = connectionFactory
        ?? throw new ArgumentNullException(nameof(connectionFactory));

    /// <inheritdoc />
    public async Task<Guid> AddAsync(SerasaPefinSolicitacaoCompleta solicitacao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitacao);

        const string sql = """
            INSERT INTO dbo.SERASA_PEFIN_SOLICITACOES
                (ID, NUM_VENDA_FK, TIPO_REGISTRO, ID_SOLICITACAO_PRINCIPAL, ID_ASSOCIADO, TIPO_ASSOCIACAO,
                 DOCUMENTO_DEVEDOR, DOCUMENTO_GARANTIDOR, DOCUMENTO_CREDOR, CONTRACT_NUMBER, CATEGORY_ID,
                 AREA_INFORMANTE, VALOR, DATA_VENCIMENTO, STATUS, TRANSACTION_ID, CADUS_KEY, CADUS_SERIE,
                 PAYLOAD_AUDITORIA, WEBHOOK_PAYLOAD, ERROR_MESSAGE, ERROR_STATUS_CODE, OPERADOR,
                 DT_CRIACAO, DT_ATUALIZACAO)
            VALUES
                (@Id, @NumVendaFk, @TipoRegistro, @IdSolicitacaoPrincipal, @IdAssociado, @TipoAssociacao,
                 @DocumentoDevedor, @DocumentoGarantidor, @DocumentoCredor, @ContractNumber, @CategoryId,
                 @AreaInformante, @Valor, @DataVencimento, @Status, @TransactionId, @CadusKey, @CadusSerie,
                 @PayloadAuditoria, @WebhookPayload, @ErrorMessage, @ErrorStatusCode, @Operador,
                 @DtCriacao, @DtAtualizacao);
            """;

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken).ConfigureAwait(false);

        try
        {
            await using var command = new SqlCommand(sql, connection, transaction);
            AddSolicitacaoParameters(command, solicitacao);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return solicitacao.Id;
        }
        catch (SqlException ex) when (ex.Number is SqlErrorDuplicateKey or SqlErrorUniqueConstraint)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw new SerasaPefinDuplicateActiveException(
                "Active Serasa PEFIN solicitation already exists for the same (NUM_VENDA, CONTRACT_NUMBER, DOCUMENTO_DEVEDOR, DOCUMENTO_GARANTIDOR, TIPO_REGISTRO) combination.",
                ex);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UpdateAsync(SerasaPefinSolicitacaoCompleta solicitacao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitacao);

        const string sql = """
            UPDATE dbo.SERASA_PEFIN_SOLICITACOES SET
                STATUS = @Status,
                TRANSACTION_ID = @TransactionId,
                CADUS_KEY = @CadusKey,
                CADUS_SERIE = @CadusSerie,
                PAYLOAD_AUDITORIA = @PayloadAuditoria,
                WEBHOOK_PAYLOAD = @WebhookPayload,
                ERROR_MESSAGE = @ErrorMessage,
                ERROR_STATUS_CODE = @ErrorStatusCode,
                DT_ATUALIZACAO = @DtAtualizacao
            WHERE ID = @Id;
            """;

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = solicitacao.Id;
        command.Parameters.Add("@Status", SqlDbType.VarChar, 32).Value = solicitacao.Status.ToDbValue();
        command.Parameters.Add("@TransactionId", SqlDbType.VarChar, 64).Value = (object?)solicitacao.TransactionId ?? DBNull.Value;
        command.Parameters.Add("@CadusKey", SqlDbType.VarChar, 64).Value = (object?)solicitacao.CadusKey ?? DBNull.Value;
        command.Parameters.Add("@CadusSerie", SqlDbType.VarChar, 64).Value = (object?)solicitacao.CadusSerie ?? DBNull.Value;
        command.Parameters.Add("@PayloadAuditoria", SqlDbType.NVarChar, -1).Value = solicitacao.PayloadAuditoria;
        command.Parameters.Add("@WebhookPayload", SqlDbType.NVarChar, -1).Value = (object?)solicitacao.WebhookPayload ?? DBNull.Value;
        command.Parameters.Add("@ErrorMessage", SqlDbType.NVarChar, 1000).Value = (object?)solicitacao.ErrorMessage ?? DBNull.Value;
        command.Parameters.Add("@ErrorStatusCode", SqlDbType.Int).Value = (object?)solicitacao.ErrorStatusCode ?? DBNull.Value;
        command.Parameters.Add("@DtAtualizacao", SqlDbType.DateTime2, 0).Value = solicitacao.DtAtualizacao;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<SerasaPefinSolicitacaoCompleta?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var sql = $"SELECT {SelectColumns} FROM dbo.SERASA_PEFIN_SOLICITACOES WHERE ID = @Id;";
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? MapReader(reader) : null;
    }

    /// <inheritdoc />
    public async Task<SerasaPefinSolicitacaoCompleta?> GetByTransactionIdAsync(string transactionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(transactionId))
        {
            return null;
        }

        var sql = $"SELECT {SelectColumns} FROM dbo.SERASA_PEFIN_SOLICITACOES WHERE TRANSACTION_ID = @TransactionId;";
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@TransactionId", SqlDbType.VarChar, 64).Value = transactionId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? MapReader(reader) : null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SerasaPefinSolicitacaoCompleta>> ListByNumVendaAsync(int numVenda, CancellationToken cancellationToken)
    {
        var sql = $"SELECT {SelectColumns} FROM dbo.SERASA_PEFIN_SOLICITACOES WHERE NUM_VENDA_FK = @NumVenda ORDER BY DT_CRIACAO DESC;";
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@NumVenda", SqlDbType.Int).Value = numVenda;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var results = new List<SerasaPefinSolicitacaoCompleta>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(MapReader(reader));
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<bool> ExistsActiveAsync(
        int numVenda,
        string contractNumber,
        string documentoDevedor,
        string? documentoGarantidor,
        SerasaPefinRecordType tipoRegistro,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP 1 1
            FROM dbo.SERASA_PEFIN_SOLICITACOES
            WHERE NUM_VENDA_FK = @NumVenda
              AND CONTRACT_NUMBER = @ContractNumber
              AND DOCUMENTO_DEVEDOR = @DocumentoDevedor
              AND TIPO_REGISTRO = @TipoRegistro
              AND STATUS IN ('PENDENTE_ENVIO', 'ENVIADO_SERASA', 'AGUARDANDO_RETORNO')
              AND ((@DocumentoGarantidor IS NULL AND DOCUMENTO_GARANTIDOR IS NULL)
                OR DOCUMENTO_GARANTIDOR = @DocumentoGarantidor);
            """;

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@NumVenda", SqlDbType.Int).Value = numVenda;
        command.Parameters.Add("@ContractNumber", SqlDbType.VarChar, 20).Value = contractNumber;
        command.Parameters.Add("@DocumentoDevedor", SqlDbType.VarChar, 20).Value = documentoDevedor;
        command.Parameters.Add("@DocumentoGarantidor", SqlDbType.VarChar, 20).Value = (object?)documentoGarantidor ?? DBNull.Value;
        command.Parameters.Add("@TipoRegistro", SqlDbType.VarChar, 20).Value = tipoRegistro.ToDbValue();

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is not null && result != DBNull.Value;
    }

    /// <inheritdoc />
    public async Task AddWebhookAsync(SerasaPefinWebhookRecord webhook, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(webhook);

        const string sql = """
            INSERT INTO dbo.SERASA_PEFIN_WEBHOOKS
                (ID, EVENT_TYPE, TRANSACTION_ID, PAYLOAD, MATCHED_SOLICITACAO_ID, PROCESSADO, MENSAGEM_ERRO, DT_RECEBIMENTO)
            VALUES
                (@Id, @EventType, @TransactionId, @Payload, @MatchedSolicitacaoId, @Processado, @MensagemErro, @DtRecebimento);
            """;

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = webhook.Id;
        command.Parameters.Add("@EventType", SqlDbType.VarChar, 64).Value = webhook.EventType;
        command.Parameters.Add("@TransactionId", SqlDbType.VarChar, 64).Value = (object?)webhook.TransactionId ?? DBNull.Value;
        command.Parameters.Add("@Payload", SqlDbType.NVarChar, -1).Value = webhook.Payload;
        command.Parameters.Add("@MatchedSolicitacaoId", SqlDbType.UniqueIdentifier).Value = (object?)webhook.MatchedSolicitacaoId ?? DBNull.Value;
        command.Parameters.Add("@Processado", SqlDbType.Bit).Value = webhook.Processado;
        command.Parameters.Add("@MensagemErro", SqlDbType.NVarChar, 1000).Value = (object?)webhook.MensagemErro ?? DBNull.Value;
        command.Parameters.Add("@DtRecebimento", SqlDbType.DateTime2, 0).Value = webhook.DtRecebimento;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> WebhookExistsByUuidAsync(string uuid, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(uuid))
        {
            return false;
        }

        const string sql = """
            SELECT TOP 1 1
            FROM dbo.SERASA_PEFIN_WEBHOOKS
            WHERE TRANSACTION_ID = @TransactionId;
            """;

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@TransactionId", SqlDbType.VarChar, 64).Value = uuid;

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is not null && result != DBNull.Value;
    }

    /// <inheritdoc />
    public async Task ApplyWebhookTransactionalAsync(
        SerasaPefinSolicitacaoCompleta solicitacao,
        SerasaPefinWebhookRecord webhook,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitacao);
        ArgumentNullException.ThrowIfNull(webhook);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken).ConfigureAwait(false);

        try
        {
            // Update solicitation
            const string updateSql = """
                UPDATE dbo.SERASA_PEFIN_SOLICITACOES SET
                    STATUS = @Status,
                    TRANSACTION_ID = @TransactionId,
                    CADUS_KEY = @CadusKey,
                    CADUS_SERIE = @CadusSerie,
                    PAYLOAD_AUDITORIA = @PayloadAuditoria,
                    WEBHOOK_PAYLOAD = @WebhookPayload,
                    ERROR_MESSAGE = @ErrorMessage,
                    ERROR_STATUS_CODE = @ErrorStatusCode,
                    DT_ATUALIZACAO = @DtAtualizacao
                WHERE ID = @Id;
                """;

            await using var updateCommand = new SqlCommand(updateSql, connection, transaction);
            updateCommand.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = solicitacao.Id;
            updateCommand.Parameters.Add("@Status", SqlDbType.VarChar, 32).Value = solicitacao.Status.ToDbValue();
            updateCommand.Parameters.Add("@TransactionId", SqlDbType.VarChar, 64).Value = (object?)solicitacao.TransactionId ?? DBNull.Value;
            updateCommand.Parameters.Add("@CadusKey", SqlDbType.VarChar, 64).Value = (object?)solicitacao.CadusKey ?? DBNull.Value;
            updateCommand.Parameters.Add("@CadusSerie", SqlDbType.VarChar, 64).Value = (object?)solicitacao.CadusSerie ?? DBNull.Value;
            updateCommand.Parameters.Add("@PayloadAuditoria", SqlDbType.NVarChar, -1).Value = solicitacao.PayloadAuditoria;
            updateCommand.Parameters.Add("@WebhookPayload", SqlDbType.NVarChar, -1).Value = (object?)solicitacao.WebhookPayload ?? DBNull.Value;
            updateCommand.Parameters.Add("@ErrorMessage", SqlDbType.NVarChar, 1000).Value = (object?)solicitacao.ErrorMessage ?? DBNull.Value;
            updateCommand.Parameters.Add("@ErrorStatusCode", SqlDbType.Int).Value = (object?)solicitacao.ErrorStatusCode ?? DBNull.Value;
            updateCommand.Parameters.Add("@DtAtualizacao", SqlDbType.DateTime2, 0).Value = solicitacao.DtAtualizacao;
            await updateCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            // Insert webhook
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

    private static void AddSolicitacaoParameters(SqlCommand command, SerasaPefinSolicitacaoCompleta s)
    {
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = s.Id;
        command.Parameters.Add("@NumVendaFk", SqlDbType.Int).Value = s.NumVendaFk;
        command.Parameters.Add("@TipoRegistro", SqlDbType.VarChar, 20).Value = s.TipoRegistro.ToDbValue();
        command.Parameters.Add("@IdSolicitacaoPrincipal", SqlDbType.UniqueIdentifier).Value = (object?)s.IdSolicitacaoPrincipal ?? DBNull.Value;
        command.Parameters.Add("@IdAssociado", SqlDbType.VarChar, 64).Value = (object?)s.IdAssociado ?? DBNull.Value;
        command.Parameters.Add("@TipoAssociacao", SqlDbType.VarChar, 64).Value = (object?)s.TipoAssociacao ?? DBNull.Value;
        command.Parameters.Add("@DocumentoDevedor", SqlDbType.VarChar, 20).Value = s.DocumentoDevedor;
        command.Parameters.Add("@DocumentoGarantidor", SqlDbType.VarChar, 20).Value = (object?)s.DocumentoGarantidor ?? DBNull.Value;
        command.Parameters.Add("@DocumentoCredor", SqlDbType.VarChar, 20).Value = s.DocumentoCredor;
        command.Parameters.Add("@ContractNumber", SqlDbType.VarChar, 20).Value = s.ContractNumber;
        command.Parameters.Add("@CategoryId", SqlDbType.Char, 2).Value = s.CategoryId;
        command.Parameters.Add("@AreaInformante", SqlDbType.VarChar, 4).Value = s.AreaInformante;
        command.Parameters.Add("@Valor", SqlDbType.Decimal).Value = s.Valor;
        command.Parameters["@Valor"].Precision = 15;
        command.Parameters["@Valor"].Scale = 2;
        command.Parameters.Add("@DataVencimento", SqlDbType.Date).Value = s.DataVencimento.ToDateTime(TimeOnly.MinValue);
        command.Parameters.Add("@Status", SqlDbType.VarChar, 32).Value = s.Status.ToDbValue();
        command.Parameters.Add("@TransactionId", SqlDbType.VarChar, 64).Value = (object?)s.TransactionId ?? DBNull.Value;
        command.Parameters.Add("@CadusKey", SqlDbType.VarChar, 64).Value = (object?)s.CadusKey ?? DBNull.Value;
        command.Parameters.Add("@CadusSerie", SqlDbType.VarChar, 64).Value = (object?)s.CadusSerie ?? DBNull.Value;
        command.Parameters.Add("@PayloadAuditoria", SqlDbType.NVarChar, -1).Value = s.PayloadAuditoria;
        command.Parameters.Add("@WebhookPayload", SqlDbType.NVarChar, -1).Value = (object?)s.WebhookPayload ?? DBNull.Value;
        command.Parameters.Add("@ErrorMessage", SqlDbType.NVarChar, 1000).Value = (object?)s.ErrorMessage ?? DBNull.Value;
        command.Parameters.Add("@ErrorStatusCode", SqlDbType.Int).Value = (object?)s.ErrorStatusCode ?? DBNull.Value;
        command.Parameters.Add("@Operador", SqlDbType.VarChar, 255).Value = s.Operador;
        command.Parameters.Add("@DtCriacao", SqlDbType.DateTime2, 0).Value = s.DtCriacao;
        command.Parameters.Add("@DtAtualizacao", SqlDbType.DateTime2, 0).Value = s.DtAtualizacao;
    }

    private static SerasaPefinSolicitacaoCompleta MapReader(SqlDataReader reader)
    {
        return SerasaPefinSolicitacaoCompleta.Hydrate(
            id: reader.GetGuid(0),
            numVendaFk: reader.GetInt32(1),
            tipoRegistro: SerasaPefinConstants.ParseRecordType(reader.GetString(2)),
            idSolicitacaoPrincipal: reader.IsDBNull(3) ? null : reader.GetGuid(3),
            idAssociado: reader.IsDBNull(4) ? null : reader.GetString(4),
            tipoAssociacao: reader.IsDBNull(5) ? null : reader.GetString(5),
            documentoDevedor: reader.GetString(6),
            documentoGarantidor: reader.IsDBNull(7) ? null : reader.GetString(7),
            documentoCredor: reader.GetString(8),
            contractNumber: reader.GetString(9),
            categoryId: reader.GetString(10),
            areaInformante: reader.GetString(11),
            valor: reader.GetDecimal(12),
            dataVencimento: DateOnly.FromDateTime(reader.GetDateTime(13)),
            status: SerasaPefinConstants.ParseStatus(reader.GetString(14)),
            transactionId: reader.IsDBNull(15) ? null : reader.GetString(15),
            cadusKey: reader.IsDBNull(16) ? null : reader.GetString(16),
            cadusSerie: reader.IsDBNull(17) ? null : reader.GetString(17),
            payloadAuditoria: reader.GetString(18),
            webhookPayload: reader.IsDBNull(19) ? null : reader.GetString(19),
            errorMessage: reader.IsDBNull(20) ? null : reader.GetString(20),
            errorStatusCode: reader.IsDBNull(21) ? null : reader.GetInt32(21),
            operador: reader.GetString(22),
            dtCriacao: reader.GetDateTime(23),
            dtAtualizacao: reader.GetDateTime(24));
    }
}
