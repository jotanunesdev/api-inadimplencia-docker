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
    private readonly SerasaPefinOptions _serasaOptions;
    private readonly NegativacaoOptions _negativacaoOptions;
    private readonly ILogger<RequestNegativacaoCommandHandler> _logger;

    public RequestNegativacaoCommandHandler(
        IInadimplenciaQueryService queryService,
        ISerasaPefinRepository repository,
        ISerasaPefinGateway gateway,
        SerasaPefinPayloadBuilder payloadBuilder,
        IOptions<SerasaPefinOptions> serasaOptions,
        IOptions<NegativacaoOptions> negativacaoOptions,
        ILogger<RequestNegativacaoCommandHandler> logger)
    {
        _queryService = queryService;
        _repository = repository;
        _gateway = gateway;
        _payloadBuilder = payloadBuilder;
        _serasaOptions = serasaOptions.Value;
        _negativacaoOptions = negativacaoOptions.Value;
        _logger = logger;
    }

    public async Task<RequestNegativacaoResponse> HandleAsync(RequestNegativacaoCommand command, CancellationToken cancellationToken = default)
    {
        var operador = command.Operador;
        var builderOptions = new SerasaPefinPayloadBuilder.Options(
            _serasaOptions.UseUatDefaults,
            _serasaOptions.AreaInformante ?? string.Empty,
            SerasaPefinConstants.CategoryId);

        _logger.LogInformation("Serasa.Inclusion.Start - NumVenda: {NumVenda}, IncluirGarantidores: {IncluirGarantidores}, Operador: {Operador}, SolicitacaoIdExistente: {SolicitacaoIdExistente}, ParcelaIds: {ParcelaIds}",
            command.NumVenda, command.IncluirGarantidores, operador, command.SolicitacaoIdExistente, command.ParcelaIds);

        // 1. Load eligible parcels from DW
        var dividasElegiveis = await _queryService.GetDividasElegiveisAsync(
            command.NumVenda,
            _negativacaoOptions.DiasAtrasoMinimo,
            cancellationToken);

        if (dividasElegiveis is null)
        {
            _logger.LogWarning("Serasa.Inclusion.DividasNotFound - NumVenda: {NumVenda}", command.NumVenda);
            throw new DomainNotFoundException($"Venda {command.NumVenda} não encontrada ou não possui dívidas elegíveis.");
        }

        // Filter parcels if ParcelaIds is provided
        IReadOnlyList<ParcelaElegivelDto> parcelasParaProcessar;
        if (command.ParcelaIds is not null && command.ParcelaIds.Count > 0)
        {
            parcelasParaProcessar = dividasElegiveis.Parcelas
                .Where(p => command.ParcelaIds.Contains(p.Id) && p.Elegivel)
                .ToList();

            if (parcelasParaProcessar.Count == 0)
            {
                _logger.LogWarning("Serasa.Inclusion.NoEligibleParcels - NumVenda: {NumVenda}, ParcelaIds: {ParcelaIds}",
                    command.NumVenda, string.Join(",", command.ParcelaIds));
                throw new DomainNotFoundException($"Nenhuma parcela elegível encontrada para os IDs fornecidos: {string.Join(", ", command.ParcelaIds)}.");
            }
        }
        else
        {
            parcelasParaProcessar = dividasElegiveis.Parcelas.Where(p => p.Elegivel).ToList();
        }

        _logger.LogInformation("Serasa.Inclusion.ParcelasLoaded - NumVenda: {NumVenda}, TotalParcelas: {Total}, Elegiveis: {Elegiveis}",
            command.NumVenda, dividasElegiveis.Parcelas.Count, parcelasParaProcessar.Count);

        // 2. Load fiadores if requested
        IReadOnlyList<FiadorQueryResult> fiadores = Array.Empty<FiadorQueryResult>();
        if (command.IncluirGarantidores)
        {
            fiadores = await _queryService.ListFiadoresAsync(command.NumVenda, cancellationToken);
            _logger.LogInformation("Serasa.Inclusion.FiadoresLoaded - NumVenda: {NumVenda}, Count: {Count}",
                command.NumVenda, fiadores.Count);
        }

        // 3. Build contract number and creditor document
        var contractNumber = dividasElegiveis.ContractNumber;
        var creditorDocument = _serasaOptions.CreditorDocument ?? string.Empty;

        var results = new List<SerasaSolicitacaoResult>();
        Guid? solicitacaoPaiId = null;

        // Reuse mode: when invoked from approval, the parent + child rows already exist.
        // We must reuse those rows (sending to Serasa and updating their status) instead of
        // creating new ones. Otherwise ExistsActiveAsync blocks every parcel and 0 sends happen.
        Dictionary<int, SerasaPefinSolicitacaoCompleta> existingPrincipalByParcela = new();
        var reuseMode = command.SolicitacaoIdExistente.HasValue;
        if (reuseMode)
        {
            solicitacaoPaiId = command.SolicitacaoIdExistente;
            var existingChildren = await _repository.ListByIdSolicitacaoPaiAsync(
                command.SolicitacaoIdExistente!.Value, cancellationToken);
            foreach (var child in existingChildren)
            {
                if (child.TipoRegistro == SerasaPefinRecordType.Principal && child.NumeroParcela.HasValue)
                {
                    existingPrincipalByParcela[child.NumeroParcela.Value] = child;
                }
            }
            _logger.LogInformation("Serasa.Inclusion.ReuseMode - NumVenda: {NumVenda}, SolicitacaoPaiId: {PaiId}, FilhasExistentes: {Count}",
                command.NumVenda, command.SolicitacaoIdExistente, existingPrincipalByParcela.Count);
        }

        // Iterate through parcels
        foreach (var parcela in parcelasParaProcessar)
        {
            _logger.LogInformation("Serasa.Inclusion.ProcessingParcela - NumVenda: {NumVenda}, ParcelaId: {ParcelaId}, NumeroParcela: {NumeroParcela}, Valor: {Valor}, Vencimento: {Vencimento}",
                command.NumVenda, parcela.Id, parcela.Id, parcela.Valor, parcela.Vencimento);

            SerasaPefinSolicitacaoCompleta? reuseEntity = null;
            if (reuseMode && existingPrincipalByParcela.TryGetValue(parcela.Id, out var existingChild))
            {
                reuseEntity = existingChild;
            }
            else if (!reuseMode)
            {
                // Idempotency check only applies when creating new rows.
                var alreadyExists = await _repository.ExistsActiveAsync(
                    command.NumVenda,
                    contractNumber,
                    dividasElegiveis.Cpf,
                    null,
                    SerasaPefinRecordType.Principal,
                    parcela.Id,
                    cancellationToken);

                if (alreadyExists)
                {
                    _logger.LogWarning("Serasa.Inclusion.ParcelaAlreadyActive - NumVenda: {NumVenda}, ParcelaId: {ParcelaId}, NumeroParcela: {NumeroParcela} - Skipping",
                        command.NumVenda, parcela.Id, parcela.Id);
                    continue;
                }
            }

            // Create parcela input for payload builder
            var parcelaInput = new ParcelaInput(
                Valor: parcela.Valor,
                Vencimento: parcela.Vencimento,
                Numero: parcela.Id,
                IdOrigem: parcela.Id.ToString());

            // Build main debt payload
            var mainDebtInput = new MainDebtInput(
                parcelaInput,
                contractNumber,
                dividasElegiveis.Cpf,
                dividasElegiveis.Cliente,
                MapAddress(dividasElegiveis.Endereco),
                creditorDocument);

            var (mainPayload, mainPayloadJson) = _payloadBuilder.BuildMainDebt(mainDebtInput, builderOptions);
            var maskedMainPayload = _payloadBuilder.SerializeMasked((Dictionary<string, object?>)mainPayload);

            _logger.LogInformation("Serasa.Inclusion.PayloadBuilt - NumVenda: {NumVenda}, ParcelaId: {ParcelaId}, NumeroParcela: {NumeroParcela}, Tipo: PRINCIPAL",
                command.NumVenda, parcela.Id, parcela.Id);

            SerasaPefinSolicitacaoCompleta principalSolicitacao;
            if (reuseEntity is not null)
            {
                // Reuse existing child row (created at solicitation time, now being approved).
                principalSolicitacao = reuseEntity;
                _logger.LogInformation("Serasa.Inclusion.Reusing - NumVenda: {NumVenda}, SolicitacaoId: {SolicitacaoId}, Tipo: PRINCIPAL, ParcelaId: {ParcelaId}",
                    command.NumVenda, principalSolicitacao.Id, parcela.Id);
            }
            else
            {
                // Create new main solicitation for this parcel
                principalSolicitacao = SerasaPefinSolicitacaoCompleta.Criar(
                    numVendaFk: command.NumVenda,
                    tipoRegistro: SerasaPefinRecordType.Principal,
                    documentoDevedor: dividasElegiveis.Cpf,
                    documentoCredor: creditorDocument,
                    contractNumber: contractNumber,
                    areaInformante: builderOptions.AreaInformante,
                    valor: parcela.Valor,
                    dataVencimento: parcela.Vencimento,
                    operador: operador,
                    payloadAuditoria: maskedMainPayload,
                    numeroParcela: parcela.Id,
                    parcelaIdOrigem: parcela.Id.ToString(),
                    idSolicitacaoPai: solicitacaoPaiId);

                try
                {
                    var solicitacaoId = await _repository.AddAsync(principalSolicitacao, cancellationToken);
                    _logger.LogInformation("Serasa.Inclusion.Persisted - NumVenda: {NumVenda}, SolicitacaoId: {SolicitacaoId}, Tipo: PRINCIPAL, ParcelaId: {ParcelaId}, NumeroParcela: {NumeroParcela}",
                        command.NumVenda, solicitacaoId, parcela.Id, parcela.Id);

                    // Set parent ID if this is the first parcel
                    if (solicitacaoPaiId is null)
                    {
                        solicitacaoPaiId = principalSolicitacao.Id;
                    }
                }
                catch (SerasaPefinDuplicateActiveException ex)
                {
                    _logger.LogWarning(ex, "Serasa.Inclusion.Duplicate - NumVenda: {NumVenda}, ParcelaId: {ParcelaId}, NumeroParcela: {NumeroParcela}",
                        command.NumVenda, parcela.Id, parcela.Id);
                    continue;
                }
            }

            // Send principal to Serasa
            try
            {
                var response = await _gateway.PostMainDebtAsync(mainPayload, cancellationToken);
                principalSolicitacao.MarcarAguardandoRetorno(response.TransactionId);
                await _repository.UpdateAsync(principalSolicitacao, cancellationToken);

                results.Add(new SerasaSolicitacaoResult(
                    principalSolicitacao.Id,
                    SerasaPefinRecordType.Principal,
                    response.TransactionId,
                    SerasaPefinStatus.AguardandoRetorno,
                    NumeroParcela: parcela.Id));

                _logger.LogInformation("Serasa.Inclusion.Sent - NumVenda: {NumVenda}, SolicitacaoId: {SolicitacaoId}, TransactionId: {TransactionId}, ParcelaId: {ParcelaId}, NumeroParcela: {NumeroParcela}",
                    command.NumVenda, principalSolicitacao.Id, response.TransactionId, parcela.Id, parcela.Id);
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
                    errorMessage,
                    NumeroParcela: parcela.Id));

                _logger.LogError(ex, "Serasa.Inclusion.HttpError - NumVenda: {NumVenda}, SolicitacaoId: {SolicitacaoId}, ParcelaId: {ParcelaId}, NumeroParcela: {NumeroParcela}",
                    command.NumVenda, principalSolicitacao.Id, parcela.Id, parcela.Id);

                // Continue to next parcel even if this one failed
            }

            // Build and send guarantors for this parcel if requested
            if (command.IncluirGarantidores && fiadores.Count > 0)
            {
                foreach (var fiador in fiadores)
                {
                    // Check idempotency for guarantor
                    var guarantorAlreadyExists = await _repository.ExistsActiveAsync(
                        command.NumVenda,
                        contractNumber,
                        dividasElegiveis.Cpf,
                        fiador.Documento,
                        SerasaPefinRecordType.Garantidor,
                        parcela.Id,
                        cancellationToken);

                    if (guarantorAlreadyExists)
                    {
                        _logger.LogWarning("Serasa.Inclusion.GuarantorAlreadyActive - NumVenda: {NumVenda}, ParcelaId: {ParcelaId}, NumeroParcela: {NumeroParcela}, Fiador: {FiadorNome} - Skipping",
                            command.NumVenda, parcela.Id, parcela.Id, fiador.Nome);
                        continue;
                    }

                    try
                    {
                        var guarantorInput = new GuarantorInput(
                            parcelaInput,
                            contractNumber,
                            dividasElegiveis.Cpf,
                            creditorDocument,
                            fiador.Documento,
                            fiador.Nome,
                            MapAddress(fiador.Endereco));

                        var (guarantorPayload, guarantorPayloadJson) = _payloadBuilder.BuildGuarantor(guarantorInput, builderOptions);
                        var maskedGuarantorPayload = _payloadBuilder.SerializeMasked((Dictionary<string, object?>)guarantorPayload);

                        var garantidorSolicitacao = SerasaPefinSolicitacaoCompleta.Criar(
                            numVendaFk: command.NumVenda,
                            tipoRegistro: SerasaPefinRecordType.Garantidor,
                            documentoDevedor: dividasElegiveis.Cpf,
                            documentoCredor: creditorDocument,
                            contractNumber: contractNumber,
                            areaInformante: builderOptions.AreaInformante,
                            valor: parcela.Valor,
                            dataVencimento: parcela.Vencimento,
                            operador: operador,
                            payloadAuditoria: maskedGuarantorPayload,
                            idSolicitacaoPrincipal: principalSolicitacao.Id,
                            documentoGarantidor: fiador.Documento,
                            idAssociado: fiador.IdAssociado,
                            tipoAssociacao: fiador.TipoAssociacao,
                            numeroParcela: parcela.Id,
                            parcelaIdOrigem: parcela.Id.ToString(),
                            idSolicitacaoPai: solicitacaoPaiId);

                        var solicitacaoId = await _repository.AddAsync(garantidorSolicitacao, cancellationToken);
                        _logger.LogInformation("Serasa.Inclusion.Persisted - NumVenda: {NumVenda}, SolicitacaoId: {SolicitacaoId}, Tipo: GARANTIDOR, Fiador: {FiadorNome}, ParcelaId: {ParcelaId}, NumeroParcela: {NumeroParcela}",
                            command.NumVenda, solicitacaoId, fiador.Nome, parcela.Id, parcela.Id);

                        var response = await _gateway.PostGuarantorAsync(guarantorPayload, cancellationToken);
                        garantidorSolicitacao.MarcarAguardandoRetorno(response.TransactionId);
                        await _repository.UpdateAsync(garantidorSolicitacao, cancellationToken);

                        results.Add(new SerasaSolicitacaoResult(
                            garantidorSolicitacao.Id,
                            SerasaPefinRecordType.Garantidor,
                            response.TransactionId,
                            SerasaPefinStatus.AguardandoRetorno,
                            NumeroParcela: parcela.Id));

                        _logger.LogInformation("Serasa.Inclusion.Sent - NumVenda: {NumVenda}, SolicitacaoId: {SolicitacaoId}, TransactionId: {TransactionId}, Fiador: {FiadorNome}, ParcelaId: {ParcelaId}, NumeroParcela: {NumeroParcela}",
                            command.NumVenda, garantidorSolicitacao.Id, response.TransactionId, fiador.Nome, parcela.Id, parcela.Id);
                    }
                    catch (SerasaPefinDuplicateActiveException ex)
                    {
                        _logger.LogWarning(ex, "Serasa.Inclusion.Duplicate - NumVenda: {NumVenda}, Fiador: {FiadorNome}, ParcelaId: {ParcelaId}, NumeroParcela: {NumeroParcela}",
                            command.NumVenda, fiador.Nome, parcela.Id, parcela.Id);
                        continue;
                    }
                    catch (Exception ex) when (ex is SerasaPefinHttpException || ex is HttpRequestException)
                    {
                        int? statusCode = ex is SerasaPefinHttpException httpEx ? httpEx.StatusCode : null;
                        var errorMessage = ex is SerasaPefinHttpException httpExMsg
                            ? $"Serasa returned HTTP {httpExMsg.StatusCode}: {httpExMsg.Body}"
                            : ex.Message;

                        _logger.LogError(ex, "Serasa.Inclusion.HttpError - NumVenda: {NumVenda}, Fiador: {FiadorNome}, ParcelaId: {ParcelaId}, NumeroParcela: {NumeroParcela}",
                            command.NumVenda, fiador.Nome, parcela.Id, parcela.Id);

                        results.Add(new SerasaSolicitacaoResult(
                            Guid.Empty,
                            SerasaPefinRecordType.Garantidor,
                            null,
                            SerasaPefinStatus.NegativadoErro,
                            errorMessage,
                            NumeroParcela: parcela.Id));

                        // Continue with next guarantor
                    }
                }
            }
        }

        // Calculate aggregated status
        var statusAgregado = CalculateAggregatedStatus(results);

        _logger.LogInformation("Serasa.Inclusion.Complete - NumVenda: {NumVenda}, StatusAgregado: {StatusAgregado}, Solicitacoes: {Count}, ParcelasProcessadas: {ParcelasProcessadas}",
            command.NumVenda, statusAgregado, results.Count, parcelasParaProcessar.Count);

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
