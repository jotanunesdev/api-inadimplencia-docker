using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Abstractions.Integrations;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Configuration;
using ApiInadimplencia.Application.Features.SerasaPefin.Dtos;
using ApiInadimplencia.Application.Features.SerasaPefin.Payloads;
using ApiInadimplencia.Domain.SerasaPefin;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApiInadimplencia.Application.Features.SerasaPefin.Commands;

/// <summary>
/// Handler for RequestNegativacaoCommand implementing complete inclusion flow:
/// 1. Load venda + fiadores from DW
/// 2. Build payloads via SerasaPefinPayloadBuilder
/// 3. Persist BEFORE sending to Serasa (SERIALIZABLE transaction)
/// 4. Send to Serasa (principal first, then guarantors)
/// 5. Update status after each response
/// </summary>
public class RequestNegativacaoCommandHandler : ICommandHandler<RequestNegativacaoCommand, RequestNegativacaoResponse>
{
    private readonly IInadimplenciaQueryService _queryService;
    private readonly ISerasaPefinRepository _repository;
    private readonly ISerasaPefinGateway _gateway;
    private readonly SerasaPefinPayloadBuilder _payloadBuilder;
    private readonly SerasaPefinOptions _options;
    private readonly ILogger<RequestNegativacaoCommandHandler> _logger;

    public RequestNegativacaoCommandHandler(
        IInadimplenciaQueryService queryService,
        ISerasaPefinRepository repository,
        ISerasaPefinGateway gateway,
        SerasaPefinPayloadBuilder payloadBuilder,
        IOptions<SerasaPefinOptions> options,
        ILogger<RequestNegativacaoCommandHandler> logger)
    {
        _queryService = queryService;
        _repository = repository;
        _gateway = gateway;
        _payloadBuilder = payloadBuilder;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<RequestNegativacaoResponse> HandleAsync(RequestNegativacaoCommand command, CancellationToken cancellationToken = default)
    {
        var operador = command.Operador;
        var builderOptions = new SerasaPefinPayloadBuilder.Options(
            _options.UseUatDefaults,
            _options.AreaInformante ?? string.Empty,
            SerasaPefinConstants.CategoryId);

        _logger.LogInformation("Serasa.Inclusion.Start - NumVenda: {NumVenda}, IncluirGarantidores: {IncluirGarantidores}, Operador: {Operador}",
            command.NumVenda, command.IncluirGarantidores, operador);

        // 1. Load venda from DW
        var venda = await _queryService.GetVendaAsync(command.NumVenda, cancellationToken);
        if (venda is null)
        {
            _logger.LogWarning("Serasa.Inclusion.VendaNotFound - NumVenda: {NumVenda}", command.NumVenda);
            throw new DomainNotFoundException($"Venda {command.NumVenda} não encontrada ou não está inadimplente.");
        }

        // 2. Load fiadores if requested
        IReadOnlyList<FiadorQueryResult> fiadores = Array.Empty<FiadorQueryResult>();
        if (command.IncluirGarantidores)
        {
            fiadores = await _queryService.ListFiadoresAsync(command.NumVenda, cancellationToken);
            _logger.LogInformation("Serasa.Inclusion.FiadoresLoaded - NumVenda: {NumVenda}, Count: {Count}",
                command.NumVenda, fiadores.Count);
        }

        // 3. Build contract number (use NumVenda as string)
        var contractNumber = command.NumVenda.ToString();
        var creditorDocument = _options.CreditorDocument ?? string.Empty;

        // 4. Build main debt payload
        var mainDebtInput = new MainDebtInput(
            venda.Valor,
            venda.DataVencimento,
            contractNumber,
            venda.DocumentoDevedor,
            venda.NomeDevedor,
            MapAddress(venda.Endereco),
            creditorDocument);

        var (mainPayload, mainPayloadJson) = _payloadBuilder.BuildMainDebt(mainDebtInput, builderOptions);
        var maskedMainPayload = _payloadBuilder.SerializeMasked((Dictionary<string, object?>)mainPayload);

        _logger.LogInformation("Serasa.Inclusion.PayloadBuilt - NumVenda: {NumVenda}, Tipo: PRINCIPAL, ContractNumber: {ContractNumber}",
            command.NumVenda, contractNumber);

        // 5. Create and persist principal solicitation (SERIALIZABLE transaction)
        var principalSolicitacao = SerasaPefinSolicitacaoCompleta.Criar(
            numVendaFk: command.NumVenda,
            tipoRegistro: SerasaPefinRecordType.Principal,
            documentoDevedor: venda.DocumentoDevedor,
            documentoCredor: creditorDocument,
            contractNumber: contractNumber,
            areaInformante: builderOptions.AreaInformante,
            valor: venda.Valor,
            dataVencimento: venda.DataVencimento,
            operador: operador,
            payloadAuditoria: maskedMainPayload);

        try
        {
            var solicitacaoId = await _repository.AddAsync(principalSolicitacao, cancellationToken);
            _logger.LogInformation("Serasa.Inclusion.Persisted - NumVenda: {NumVenda}, SolicitacaoId: {SolicitacaoId}, Tipo: PRINCIPAL",
                command.NumVenda, solicitacaoId);
        }
        catch (SerasaPefinDuplicateActiveException ex)
        {
            _logger.LogWarning(ex, "Serasa.Inclusion.Duplicate - NumVenda: {NumVenda}", command.NumVenda);
            throw;
        }

        var results = new List<SerasaSolicitacaoResult>();

        // 6. Send principal to Serasa
        try
        {
            var response = await _gateway.PostMainDebtAsync(mainPayload, cancellationToken);
            principalSolicitacao.MarcarAguardandoRetorno(response.TransactionId);
            await _repository.UpdateAsync(principalSolicitacao, cancellationToken);

            results.Add(new SerasaSolicitacaoResult(
                principalSolicitacao.Id,
                SerasaPefinRecordType.Principal,
                response.TransactionId,
                SerasaPefinStatus.AguardandoRetorno));

            _logger.LogInformation("Serasa.Inclusion.Sent - NumVenda: {NumVenda}, SolicitacaoId: {SolicitacaoId}, TransactionId: {TransactionId}",
                command.NumVenda, principalSolicitacao.Id, response.TransactionId);
        }
        catch (Exception ex) when (ex is SerasaPefinHttpException || ex is HttpRequestException)
        {
            int? statusCode = ex is SerasaPefinHttpException httpEx ? httpEx.StatusCode : null;
            var errorMessage = ex is SerasaPefinHttpException httpExMsg
                ? $"Serasa returned HTTP {httpExMsg.StatusCode}: {httpExMsg.Body}"
                : ex.Message;

            principalSolicitacao.MarcarFalhaEnvio(errorMessage, statusCode);
            await _repository.UpdateAsync(principalSolicitacao, cancellationToken);

            results.Add(new SerasaSolicitacaoResult(
                principalSolicitacao.Id,
                SerasaPefinRecordType.Principal,
                null,
                SerasaPefinStatus.NegativadoErro,
                errorMessage));

            _logger.LogError(ex, "Serasa.Inclusion.HttpError - NumVenda: {NumVenda}, SolicitacaoId: {SolicitacaoId}",
                command.NumVenda, principalSolicitacao.Id);

            // Continue to try guarantors even if principal failed
        }

        // 7. Build and send guarantors if requested
        if (command.IncluirGarantidores && fiadores.Count > 0)
        {
            foreach (var fiador in fiadores)
            {
                try
                {
                    var guarantorInput = new GuarantorInput(
                        venda.Valor,
                        venda.DataVencimento,
                        contractNumber,
                        venda.DocumentoDevedor,
                        creditorDocument,
                        fiador.Documento,
                        fiador.Nome,
                        MapAddress(fiador.Endereco));

                    var (guarantorPayload, guarantorPayloadJson) = _payloadBuilder.BuildGuarantor(guarantorInput, builderOptions);
                    var maskedGuarantorPayload = _payloadBuilder.SerializeMasked((Dictionary<string, object?>)guarantorPayload);

                    var garantidorSolicitacao = SerasaPefinSolicitacaoCompleta.Criar(
                        numVendaFk: command.NumVenda,
                        tipoRegistro: SerasaPefinRecordType.Garantidor,
                        documentoDevedor: venda.DocumentoDevedor,
                        documentoCredor: creditorDocument,
                        contractNumber: contractNumber,
                        areaInformante: builderOptions.AreaInformante,
                        valor: venda.Valor,
                        dataVencimento: venda.DataVencimento,
                        operador: operador,
                        payloadAuditoria: maskedGuarantorPayload,
                        idSolicitacaoPrincipal: principalSolicitacao.Id,
                        documentoGarantidor: fiador.Documento,
                        idAssociado: fiador.IdAssociado,
                        tipoAssociacao: fiador.TipoAssociacao);

                    var solicitacaoId = await _repository.AddAsync(garantidorSolicitacao, cancellationToken);
                    _logger.LogInformation("Serasa.Inclusion.Persisted - NumVenda: {NumVenda}, SolicitacaoId: {SolicitacaoId}, Tipo: GARANTIDOR, Fiador: {FiadorNome}",
                        command.NumVenda, solicitacaoId, fiador.Nome);

                    var response = await _gateway.PostGuarantorAsync(guarantorPayload, cancellationToken);
                    garantidorSolicitacao.MarcarAguardandoRetorno(response.TransactionId);
                    await _repository.UpdateAsync(garantidorSolicitacao, cancellationToken);

                    results.Add(new SerasaSolicitacaoResult(
                        garantidorSolicitacao.Id,
                        SerasaPefinRecordType.Garantidor,
                        response.TransactionId,
                        SerasaPefinStatus.AguardandoRetorno));

                    _logger.LogInformation("Serasa.Inclusion.Sent - NumVenda: {NumVenda}, SolicitacaoId: {SolicitacaoId}, TransactionId: {TransactionId}, Fiador: {FiadorNome}",
                        command.NumVenda, garantidorSolicitacao.Id, response.TransactionId, fiador.Nome);
                }
                catch (SerasaPefinDuplicateActiveException ex)
                {
                    _logger.LogWarning(ex, "Serasa.Inclusion.Duplicate - NumVenda: {NumVenda}, Fiador: {FiadorNome}",
                        command.NumVenda, fiador.Nome);
                    // Continue with next guarantor
                }
                catch (Exception ex) when (ex is SerasaPefinHttpException || ex is HttpRequestException)
                {
                    int? statusCode = ex is SerasaPefinHttpException httpEx ? httpEx.StatusCode : null;
                    var errorMessage = ex is SerasaPefinHttpException httpExMsg
                        ? $"Serasa returned HTTP {httpExMsg.StatusCode}: {httpExMsg.Body}"
                        : ex.Message;

                    _logger.LogError(ex, "Serasa.Inclusion.HttpError - NumVenda: {NumVenda}, Fiador: {FiadorNome}",
                        command.NumVenda, fiador.Nome);

                    results.Add(new SerasaSolicitacaoResult(
                        Guid.Empty,
                        SerasaPefinRecordType.Garantidor,
                        null,
                        SerasaPefinStatus.NegativadoErro,
                        errorMessage));

                    // Continue with next guarantor
                }
            }
        }

        // 8. Calculate aggregated status
        var statusAgregado = CalculateAggregatedStatus(results);

        _logger.LogInformation("Serasa.Inclusion.Complete - NumVenda: {NumVenda}, StatusAgregado: {StatusAgregado}, Solicitacoes: {Count}",
            command.NumVenda, statusAgregado, results.Count);

        return new RequestNegativacaoResponse(results, statusAgregado);
    }

    private static SerasaAddress? MapAddress(EnderecoDto? endereco)
    {
        if (endereco is null) return null;

        return new SerasaAddress(
            endereco.ZipCode,
            endereco.AddressLine,
            endereco.District,
            endereco.City,
            endereco.State,
            endereco.Complement,
            endereco.Number);
    }

    private static SerasaPefinStatus CalculateAggregatedStatus(IReadOnlyList<SerasaSolicitacaoResult> results)
    {
        if (results.Count == 0) return SerasaPefinStatus.PendenteEnvio;

        var anyError = results.Any(r => r.Status == SerasaPefinStatus.NegativadoErro);
        var anySuccess = results.Any(r => r.Status == SerasaPefinStatus.AguardandoRetorno);

        if (anyError && anySuccess) return SerasaPefinStatus.NegativadoErro;
        if (anyError) return SerasaPefinStatus.NegativadoErro;
        if (anySuccess) return SerasaPefinStatus.AguardandoRetorno;

        return SerasaPefinStatus.PendenteEnvio;
    }
}

/// <summary>
/// Exception thrown when a domain entity is not found (e.g., venda not in DW).
/// </summary>
public sealed class DomainNotFoundException : Exception
{
    public DomainNotFoundException(string message) : base(message)
    {
    }

    public DomainNotFoundException(string message, Exception inner) : base(message, inner)
    {
    }
}
