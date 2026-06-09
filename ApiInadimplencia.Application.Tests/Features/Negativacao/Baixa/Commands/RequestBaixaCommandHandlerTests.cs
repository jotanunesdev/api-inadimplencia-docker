using ApiInadimplencia.Application.Abstractions;
using ApiInadimplencia.Application.Abstractions.Auth;
using ApiInadimplencia.Application.Abstractions.Integrations;
using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Configuration;
using ApiInadimplencia.Application.Features.Negativacao.Baixa.Commands;
using ApiInadimplencia.Application.Features.Notifications;
using ApiInadimplencia.Application.Features.Ocorrencias;
using ApiInadimplencia.Domain.Notifications;
using ApiInadimplencia.Domain.Ocorrencias;
using ApiInadimplencia.Domain.SerasaPefin;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ApiInadimplencia.Application.Tests.Features.Negativacao.Baixa.Commands;

/// <summary>
/// Testes para <c>RequestBaixaCommandHandler</c>:
/// senha (3 estados), parcela inexistente, parcela sem negativação_sucesso,
/// duplicidade ativa, sucesso (cria N agregados, ocorrência, notificações).
/// </summary>
public sealed class RequestBaixaCommandHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<ISenhaTransacaoValidator> _senhaValidatorMock = new();
    private readonly Mock<IInadimplenciaQueryService> _queryServiceMock = new();
    private readonly Mock<ISerasaPefinRepository> _serasaRepoMock = new();
    private readonly Mock<ISerasaPefinBaixaRepository> _baixaRepoMock = new();
    private readonly Mock<IOcorrenciaRepository> _ocorrenciaRepoMock = new();
    private readonly Mock<IProtocoloGenerator> _protocoloMock = new();
    private readonly Mock<IAprovadoresPolicy> _aprovadoresMock = new();
    private readonly Mock<INotificationDispatcher> _notificationMock = new();
    private readonly Mock<ISerasaPefinGateway> _serasaGatewayMock = new();
    private readonly SerasaPefinOptions _serasaOptions = new() { CreditorDocument = "16202491000193" };
    private readonly Mock<IInadimplenciaParcelaWriteService> _parcelaWriterMock = new();

    private readonly RequestBaixaCommandHandler _handler;

    public RequestBaixaCommandHandlerTests()
    {
        _handler = new RequestBaixaCommandHandler(
            _currentUserMock.Object,
            _senhaValidatorMock.Object,
            _queryServiceMock.Object,
            _serasaRepoMock.Object,
            _baixaRepoMock.Object,
            _ocorrenciaRepoMock.Object,
            _protocoloMock.Object,
            _aprovadoresMock.Object,
            _notificationMock.Object,
            _serasaGatewayMock.Object,
            Microsoft.Extensions.Options.Options.Create(_serasaOptions),
            _parcelaWriterMock.Object,
            NullLogger<RequestBaixaCommandHandler>.Instance);
    }

    private void SetupAuthenticatedUser(string username = "operador")
    {
        _currentUserMock.SetupGet(s => s.Username).Returns(username);
        _currentUserMock.SetupGet(s => s.IsAuthenticated).Returns(true);
    }

    private static RequestBaixaCommand BuildCommand(
        int numVenda = 12345,
        IReadOnlyList<int>? parcelaIds = null,
        byte motivo = 3,
        string senha = "senha_correta",
        string? justificativa = "Cliente quitou as parcelas") =>
        new(numVenda, parcelaIds ?? new[] { 1, 2 }, motivo, senha, justificativa);

    /// <summary>
    /// Cria um child de <c>SerasaPefinSolicitacaoCompleta</c> hidratado simulando
    /// uma parcela já com a integração completa. <paramref name="status"/> permite
    /// simular cenários onde a parcela ainda não foi negativada com sucesso.
    /// </summary>
    private static SerasaPefinSolicitacaoCompleta BuildChild(
        int numeroParcela,
        int numVenda = 12345,
        SerasaPefinStatus status = SerasaPefinStatus.NegativadoSucesso,
        string contractNumber = "CTR-12345",
        string documentoDevedor = "12345678901",
        string documentoCredor = "98765432100123") =>
        SerasaPefinSolicitacaoCompleta.Hydrate(
            id: Guid.NewGuid(),
            numVendaFk: numVenda,
            tipoRegistro: SerasaPefinRecordType.Principal,
            idSolicitacaoPrincipal: null,
            idAssociado: null,
            tipoAssociacao: null,
            documentoDevedor: documentoDevedor,
            documentoGarantidor: null,
            documentoCredor: documentoCredor,
            contractNumber: contractNumber,
            categoryId: "FI",
            areaInformante: "0001",
            valor: 1000m,
            dataVencimento: new DateOnly(2024, 1, 1),
            status: status,
            transactionId: "uuid-1",
            cadusKey: null,
            cadusSerie: null,
            payloadAuditoria: "{}",
            webhookPayload: null,
            errorMessage: null,
            errorStatusCode: null,
            operador: "operador",
            dtCriacao: DateTime.UtcNow,
            dtAtualizacao: DateTime.UtcNow,
            numeroParcela: numeroParcela,
            parcelaIdOrigem: numeroParcela.ToString(),
            idSolicitacaoPai: Guid.NewGuid());

    private void VerifyNoWrites()
    {
        _baixaRepoMock.Verify(r => r.AddAsync(It.IsAny<SerasaPefinBaixaSolicitacao>(), It.IsAny<CancellationToken>()), Times.Never);
        _baixaRepoMock.Verify(r => r.AddManyAsync(It.IsAny<IReadOnlyCollection<SerasaPefinBaixaSolicitacao>>(), It.IsAny<CancellationToken>()), Times.Never);
        _ocorrenciaRepoMock.Verify(r => r.AddAsync(It.IsAny<Ocorrencia>(), It.IsAny<CancellationToken>()), Times.Never);
        _notificationMock.Verify(
            n => n.DispatchManyAsync(
                It.IsAny<NotificationType>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<int?>(),
                It.IsAny<string>(),
                It.IsAny<DateOnly?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ---------------------------------------------------------------------
    // SENHA
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(SenhaTransacaoValidationResult.Invalid, "SENHA_INVALIDA")]
    [InlineData(SenhaTransacaoValidationResult.LockedOut, "SENHA_BLOQUEADA")]
    [InlineData(SenhaTransacaoValidationResult.NotSet, "SENHA_NAO_CADASTRADA")]
    public async Task HandleAsync_SenhaInvalidaOuBloqueadaOuNaoCadastrada_DeveLancarSemEscritas(
        SenhaTransacaoValidationResult resultadoSenha,
        string mensagemEsperada)
    {
        SetupAuthenticatedUser();
        _senhaValidatorMock.Setup(v => v.ValidateAsync("operador", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultadoSenha);

        var act = () => _handler.HandleAsync(BuildCommand(), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<UnauthorizedAccessException>();
        ex.Which.Message.Should().Contain(mensagemEsperada);
        VerifyNoWrites();
    }

    [Fact]
    public async Task HandleAsync_UsuarioNaoAutenticado_DeveLancar()
    {
        _currentUserMock.SetupGet(s => s.IsAuthenticated).Returns(false);

        var act = () => _handler.HandleAsync(BuildCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        VerifyNoWrites();
    }

    // ---------------------------------------------------------------------
    // MOTIVO
    // ---------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_MotivoForaDaWhitelist_DeveLancar()
    {
        SetupAuthenticatedUser();
        _senhaValidatorMock.Setup(v => v.ValidateAsync("operador", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SenhaTransacaoValidationResult.Valid);

        var command = BuildCommand(motivo: 99);

        var act = () => _handler.HandleAsync(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*motivo*");
        VerifyNoWrites();
    }

    // ---------------------------------------------------------------------
    // PARCELAS
    // ---------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_ParcelaSemNegativacaoSucesso_DeveLancarNaoElegivel()
    {
        SetupAuthenticatedUser();
        _senhaValidatorMock.Setup(v => v.ValidateAsync("operador", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SenhaTransacaoValidationResult.Valid);

        // Parcela 1 está negativada com sucesso, mas parcela 2 está em PENDENTE_ENVIO.
        _serasaRepoMock.Setup(r => r.ListByNumVendaAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                BuildChild(1, status: SerasaPefinStatus.NegativadoSucesso),
                BuildChild(2, status: SerasaPefinStatus.PendenteEnvio),
            });

        var act = () => _handler.HandleAsync(BuildCommand(parcelaIds: new[] { 1, 2 }), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*NAO_ELEGIVEL*");
        VerifyNoWrites();
    }

    [Fact]
    public async Task HandleAsync_ParcelaInexistenteParaVenda_DeveLancarNaoElegivel()
    {
        SetupAuthenticatedUser();
        _senhaValidatorMock.Setup(v => v.ValidateAsync("operador", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SenhaTransacaoValidationResult.Valid);

        // Apenas parcela 1 existe; usuário pediu 1 e 2.
        _serasaRepoMock.Setup(r => r.ListByNumVendaAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { BuildChild(1) });

        var act = () => _handler.HandleAsync(BuildCommand(parcelaIds: new[] { 1, 2 }), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*NAO_ELEGIVEL*");
        VerifyNoWrites();
    }

    [Fact]
    public async Task HandleAsync_ParcelaIdsVazio_DeveLancar()
    {
        SetupAuthenticatedUser();
        _senhaValidatorMock.Setup(v => v.ValidateAsync("operador", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SenhaTransacaoValidationResult.Valid);

        var act = () => _handler.HandleAsync(BuildCommand(parcelaIds: Array.Empty<int>()), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
        VerifyNoWrites();
    }

    // ---------------------------------------------------------------------
    // DUPLICIDADE
    // ---------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_BaixaAtivaJaExistenteParaParcela_DeveLancarJaEmAprovacao()
    {
        SetupAuthenticatedUser();
        _senhaValidatorMock.Setup(v => v.ValidateAsync("operador", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SenhaTransacaoValidationResult.Valid);

        _serasaRepoMock.Setup(r => r.ListByNumVendaAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { BuildChild(1), BuildChild(2) });

        // Parcela 1 já tem baixa ativa.
        _baixaRepoMock.Setup(r => r.ExistsActiveAsync(12345, "CTR-12345", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _baixaRepoMock.Setup(r => r.ExistsActiveAsync(12345, "CTR-12345", 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var act = () => _handler.HandleAsync(BuildCommand(parcelaIds: new[] { 1, 2 }), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*JA_EM_APROVACAO*");
        VerifyNoWrites();
    }

    // ---------------------------------------------------------------------
    // SUCESSO
    // ---------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_Sucesso_CriaNAgregadosOcorrenciaENotificaAprovadores()
    {
        SetupAuthenticatedUser();
        _senhaValidatorMock.Setup(v => v.ValidateAsync("operador", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SenhaTransacaoValidationResult.Valid);

        var children = new[] { BuildChild(1), BuildChild(2) };
        _serasaRepoMock.Setup(r => r.ListByNumVendaAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(children);

        _baixaRepoMock.Setup(r => r.ExistsActiveAsync(12345, "CTR-12345", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var venda = new InadimplenciaQueryResult(
            NumVenda: 12345,
            DocumentoDevedor: "12345678901",
            NomeDevedor: "João Silva",
            Cliente: "João Silva",
            Empreendimento: "Empreendimento A",
            Bloco: "Bloco 1",
            Unidade: "Apto 101",
            Valor: 2000m,
            DataVencimento: new DateOnly(2024, 1, 1),
            Endereco: null);
        _queryServiceMock.Setup(q => q.GetVendaAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(venda);

        _protocoloMock.Setup(p => p.GerarProtocoloAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("BX-202606030001");

        var aprovadores = new[] { "aprovador1", "aprovador2" };
        _aprovadoresMock.Setup(a => a.ListAprovadores()).Returns(aprovadores);

        _notificationMock.Setup(n => n.DispatchManyAsync(
                It.IsAny<NotificationType>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<int?>(),
                It.IsAny<string>(),
                It.IsAny<DateOnly?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Guid?>());

        IReadOnlyCollection<SerasaPefinBaixaSolicitacao>? captured = null;
        _baixaRepoMock.Setup(r => r.AddManyAsync(It.IsAny<IReadOnlyCollection<SerasaPefinBaixaSolicitacao>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<SerasaPefinBaixaSolicitacao>, CancellationToken>((items, _) => captured = items)
            .Returns(Task.CompletedTask);

        var result = await _handler.HandleAsync(BuildCommand(parcelaIds: new[] { 1, 2 }, motivo: 3), CancellationToken.None);

        result.Should().NotBe(Guid.Empty);
        captured.Should().NotBeNull();
        captured!.Should().HaveCount(2);
        captured.All(b => b.Status == SerasaPefinBaixaStatus.AguardandoAprovacao).Should().BeTrue();
        captured.All(b => b.SolicitanteUsername == "operador").Should().BeTrue();
        captured.All(b => b.Motivo.Codigo == 3).Should().BeTrue();
        captured.All(b => b.NumVendaFk == 12345 && b.ContractNumber == "CTR-12345").Should().BeTrue();
        captured.Select(b => b.NumeroParcela).Should().BeEquivalentTo(new int?[] { 1, 2 });
        captured.Select(b => b.IdSolicitacaoNegativacao).Should().BeEquivalentTo(children.Select(c => c.Id));

        _ocorrenciaRepoMock.Verify(r => r.AddAsync(
            It.Is<Ocorrencia>(o =>
                o.NumVendaFk == 12345 &&
                o.NomeUsuarioFk == "operador" &&
                o.StatusOcorrencia == "Solicitação de baixa" &&
                o.Descricao.Contains("baixa")),
            It.IsAny<CancellationToken>()), Times.Once);

        _notificationMock.Verify(n => n.DispatchManyAsync(
            NotificationType.SolicitacaoBaixa,
            aprovadores,
            12345,
            It.IsAny<string>(),
            It.IsAny<DateOnly?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_VerificaExistsActiveAntesDePersistir()
    {
        SetupAuthenticatedUser();
        _senhaValidatorMock.Setup(v => v.ValidateAsync("operador", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SenhaTransacaoValidationResult.Valid);

        _serasaRepoMock.Setup(r => r.ListByNumVendaAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { BuildChild(1) });

        var sequence = new MockSequence();
        _baixaRepoMock.InSequence(sequence)
            .Setup(r => r.ExistsActiveAsync(12345, "CTR-12345", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _baixaRepoMock.InSequence(sequence)
            .Setup(r => r.AddManyAsync(It.IsAny<IReadOnlyCollection<SerasaPefinBaixaSolicitacao>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _queryServiceMock.Setup(q => q.GetVendaAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InadimplenciaQueryResult(
                12345, "12345678901", "Cliente", "Cliente", "Emp", "B1", "U1", 1000m, new DateOnly(2024, 1, 1), null));
        _protocoloMock.Setup(p => p.GerarProtocoloAsync(It.IsAny<CancellationToken>())).ReturnsAsync("BX1");
        _aprovadoresMock.Setup(a => a.ListAprovadores()).Returns(Array.Empty<string>());

        await _handler.HandleAsync(BuildCommand(parcelaIds: new[] { 1 }), CancellationToken.None);

        _baixaRepoMock.Verify(r => r.ExistsActiveAsync(12345, "CTR-12345", 1, It.IsAny<CancellationToken>()), Times.Once);
        _baixaRepoMock.Verify(r => r.AddManyAsync(It.IsAny<IReadOnlyCollection<SerasaPefinBaixaSolicitacao>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
