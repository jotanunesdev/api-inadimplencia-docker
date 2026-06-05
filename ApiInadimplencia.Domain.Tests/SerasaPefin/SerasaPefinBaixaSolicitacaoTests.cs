using ApiInadimplencia.Domain.SerasaPefin;
using FluentAssertions;
using Xunit;

namespace ApiInadimplencia.Domain.Tests.SerasaPefin;

/// <summary>
/// Cobertura do aggregate <see cref="SerasaPefinBaixaSolicitacao"/>:
/// factory <c>CriarParaAprovacao</c> + cada transição válida e inválida +
/// limite de tentativas no reenvio.
/// </summary>
public sealed class SerasaPefinBaixaSolicitacaoTests
{
    private static SerasaPefinBaixaSolicitacao CriarValida(
        byte motivoCodigo = 3,
        Guid? idSolicitacaoNegativacao = null,
        int numVendaFk = 12345,
        int? numeroParcela = 2,
        string contractNumber = "12345",
        string documentoDevedor = "12345678901",
        string documentoCredor = "98765432100123",
        string solicitanteUsername = "solicitante")
    {
        return SerasaPefinBaixaSolicitacao.CriarParaAprovacao(
            idSolicitacaoNegativacao: idSolicitacaoNegativacao ?? Guid.NewGuid(),
            numVendaFk: numVendaFk,
            numeroParcela: numeroParcela,
            contractNumber: contractNumber,
            documentoDevedor: documentoDevedor,
            documentoCredor: documentoCredor,
            motivo: SerasaPefinBaixaMotivo.From(motivoCodigo),
            solicitanteUsername: solicitanteUsername);
    }

    // ---------------------------------------------------------------------
    // FACTORY
    // ---------------------------------------------------------------------

    [Fact]
    public void CriarParaAprovacao_CamposObrigatoriosValidos_RetornaAggregateEmAguardandoAprovacao()
    {
        var idSolicitacaoNegativacao = Guid.NewGuid();
        var motivo = SerasaPefinBaixaMotivo.From(3);

        var baixa = SerasaPefinBaixaSolicitacao.CriarParaAprovacao(
            idSolicitacaoNegativacao: idSolicitacaoNegativacao,
            numVendaFk: 100,
            numeroParcela: 2,
            contractNumber: "100",
            documentoDevedor: "123.456.789-01",
            documentoCredor: "98.765.432/0001-23",
            motivo: motivo,
            solicitanteUsername: "solicitante");

        baixa.Id.Should().NotBeEmpty();
        baixa.IdSolicitacaoNegativacao.Should().Be(idSolicitacaoNegativacao);
        baixa.NumVendaFk.Should().Be(100);
        baixa.NumeroParcela.Should().Be(2);
        baixa.ContractNumber.Should().Be("100");
        baixa.DocumentoDevedor.Should().Be("12345678901"); // digits-only
        baixa.DocumentoCredor.Should().Be("98765432000123"); // digits-only
        baixa.Motivo.Should().Be(motivo);
        baixa.Status.Should().Be(SerasaPefinBaixaStatus.AguardandoAprovacao);
        baixa.SolicitanteUsername.Should().Be("solicitante");
        baixa.Tentativas.Should().Be(1);
        baixa.AprovadorUsername.Should().BeNull();
        baixa.DtAprovacao.Should().BeNull();
        baixa.TransactionId.Should().BeNull();
    }

    [Fact]
    public void CriarParaAprovacao_NumVendaInvalido_DeveLancar()
    {
        Action act = () => SerasaPefinBaixaSolicitacao.CriarParaAprovacao(
            idSolicitacaoNegativacao: Guid.NewGuid(),
            numVendaFk: 0,
            numeroParcela: 1,
            contractNumber: "1",
            documentoDevedor: "12345678901",
            documentoCredor: "98765432100123",
            motivo: SerasaPefinBaixaMotivo.From(3),
            solicitanteUsername: "solicitante");

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("numVendaFk");
    }

    [Fact]
    public void CriarParaAprovacao_NumeroParcelaNaoPositivo_DeveLancar()
    {
        Action act = () => CriarValida(numeroParcela: 0);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("numeroParcela");
    }

    [Fact]
    public void CriarParaAprovacao_NumeroParcelaNuloEhAceito()
    {
        var baixa = CriarValida(numeroParcela: null);

        baixa.NumeroParcela.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CriarParaAprovacao_ContractNumberInvalido_DeveLancar(string contract)
    {
        Action act = () => CriarValida(contractNumber: contract);

        act.Should().Throw<ArgumentException>().WithParameterName("contractNumber");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CriarParaAprovacao_DocumentoDevedorInvalido_DeveLancar(string documento)
    {
        Action act = () => CriarValida(documentoDevedor: documento);

        act.Should().Throw<ArgumentException>().WithParameterName("documentoDevedor");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CriarParaAprovacao_DocumentoCredorInvalido_DeveLancar(string documento)
    {
        Action act = () => CriarValida(documentoCredor: documento);

        act.Should().Throw<ArgumentException>().WithParameterName("documentoCredor");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CriarParaAprovacao_SolicitanteVazio_DeveLancar(string solicitante)
    {
        Action act = () => CriarValida(solicitanteUsername: solicitante);

        act.Should().Throw<ArgumentException>().WithParameterName("solicitanteUsername");
    }

    [Fact]
    public void CriarParaAprovacao_IdSolicitacaoNegativacaoVazio_DeveLancar()
    {
        Action act = () => CriarValida(idSolicitacaoNegativacao: Guid.Empty);

        act.Should().Throw<ArgumentException>().WithParameterName("idSolicitacaoNegativacao");
    }

    // ---------------------------------------------------------------------
    // MarcarAprovada
    // ---------------------------------------------------------------------

    [Fact]
    public void MarcarAprovada_FromAguardandoAprovacao_TransicionaParaAprovada()
    {
        var baixa = CriarValida();
        var utcNow = DateTime.UtcNow;

        baixa.MarcarAprovada("aprovador", utcNow);

        baixa.Status.Should().Be(SerasaPefinBaixaStatus.Aprovada);
        baixa.AprovadorUsername.Should().Be("aprovador");
        baixa.DtAprovacao.Should().Be(utcNow);
    }

    [Fact]
    public void MarcarAprovada_FromOutroEstado_DeveLancar()
    {
        var baixa = CriarValida();
        baixa.MarcarRejeitada("aprovador", "n/a", DateTime.UtcNow);

        Action act = () => baixa.MarcarAprovada("aprovador", DateTime.UtcNow);

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MarcarAprovada_AprovadorVazio_DeveLancar(string aprovador)
    {
        var baixa = CriarValida();

        Action act = () => baixa.MarcarAprovada(aprovador, DateTime.UtcNow);

        act.Should().Throw<ArgumentException>().WithParameterName("aprovadorUsername");
    }

    // ---------------------------------------------------------------------
    // MarcarRejeitada
    // ---------------------------------------------------------------------

    [Fact]
    public void MarcarRejeitada_FromAguardandoAprovacao_TransicionaParaRejeitada()
    {
        var baixa = CriarValida();
        var utcNow = DateTime.UtcNow;

        baixa.MarcarRejeitada("aprovador", "valor incorreto", utcNow);

        baixa.Status.Should().Be(SerasaPefinBaixaStatus.Rejeitada);
        baixa.AprovadorUsername.Should().Be("aprovador");
        baixa.Justificativa.Should().Be("valor incorreto");
        baixa.DtAprovacao.Should().Be(utcNow);
    }

    [Fact]
    public void MarcarRejeitada_FromOutroEstado_DeveLancar()
    {
        var baixa = CriarValida();
        baixa.MarcarAprovada("aprovador", DateTime.UtcNow);

        Action act = () => baixa.MarcarRejeitada("aprovador", "x", DateTime.UtcNow);

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MarcarRejeitada_JustificativaVazia_DeveLancar(string justificativa)
    {
        var baixa = CriarValida();

        Action act = () => baixa.MarcarRejeitada("aprovador", justificativa, DateTime.UtcNow);

        act.Should().Throw<ArgumentException>().WithParameterName("justificativa");
    }

    // ---------------------------------------------------------------------
    // MarcarPendenteEnvio
    // ---------------------------------------------------------------------

    [Fact]
    public void MarcarPendenteEnvio_FromAprovada_TransicionaParaPendenteEnvio()
    {
        var baixa = CriarValida();
        baixa.MarcarAprovada("aprovador", DateTime.UtcNow);

        baixa.MarcarPendenteEnvio();

        baixa.Status.Should().Be(SerasaPefinBaixaStatus.PendenteEnvio);
    }

    [Fact]
    public void MarcarPendenteEnvio_FromAguardandoAprovacao_DeveLancar()
    {
        var baixa = CriarValida();

        Action act = () => baixa.MarcarPendenteEnvio();

        act.Should().Throw<InvalidOperationException>();
    }

    // ---------------------------------------------------------------------
    // MarcarBaixaAguardandoRetorno
    // ---------------------------------------------------------------------

    [Fact]
    public void MarcarBaixaAguardandoRetorno_FromPendenteEnvio_TransicionaECarregaTransactionId()
    {
        var baixa = CriarValida();
        baixa.MarcarAprovada("aprovador", DateTime.UtcNow);
        baixa.MarcarPendenteEnvio();

        baixa.MarcarBaixaAguardandoRetorno("uuid-1");

        baixa.Status.Should().Be(SerasaPefinBaixaStatus.BaixaAguardandoRetorno);
        baixa.TransactionId.Should().Be("uuid-1");
    }

    [Fact]
    public void MarcarBaixaAguardandoRetorno_FromOutroEstado_DeveLancar()
    {
        var baixa = CriarValida();

        Action act = () => baixa.MarcarBaixaAguardandoRetorno("uuid-1");

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MarcarBaixaAguardandoRetorno_TransactionIdVazio_DeveLancar(string transactionId)
    {
        var baixa = CriarValida();
        baixa.MarcarAprovada("aprovador", DateTime.UtcNow);
        baixa.MarcarPendenteEnvio();

        Action act = () => baixa.MarcarBaixaAguardandoRetorno(transactionId);

        act.Should().Throw<ArgumentException>().WithParameterName("transactionId");
    }

    // ---------------------------------------------------------------------
    // AplicarWebhookSucesso
    // ---------------------------------------------------------------------

    [Fact]
    public void AplicarWebhookSucesso_FromBaixaAguardandoRetorno_TransicionaParaBaixadoSucesso()
    {
        var baixa = CriarValida();
        baixa.MarcarAprovada("aprovador", DateTime.UtcNow);
        baixa.MarcarPendenteEnvio();
        baixa.MarcarBaixaAguardandoRetorno("uuid-1");

        baixa.AplicarWebhookSucesso("{\"ok\":true}");

        baixa.Status.Should().Be(SerasaPefinBaixaStatus.BaixadoSucesso);
        baixa.WebhookPayload.Should().Be("{\"ok\":true}");
    }

    [Fact]
    public void AplicarWebhookSucesso_FromOutroEstado_DeveLancar()
    {
        var baixa = CriarValida();

        Action act = () => baixa.AplicarWebhookSucesso("{}");

        act.Should().Throw<InvalidOperationException>();
    }

    // ---------------------------------------------------------------------
    // AplicarWebhookErro
    // ---------------------------------------------------------------------

    [Fact]
    public void AplicarWebhookErro_FromBaixaAguardandoRetorno_TransicionaParaBaixadoErro()
    {
        var baixa = CriarValida();
        baixa.MarcarAprovada("aprovador", DateTime.UtcNow);
        baixa.MarcarPendenteEnvio();
        baixa.MarcarBaixaAguardandoRetorno("uuid-1");

        baixa.AplicarWebhookErro("{\"err\":\"x\"}", "X", 400);

        baixa.Status.Should().Be(SerasaPefinBaixaStatus.BaixadoErro);
        baixa.WebhookPayload.Should().Be("{\"err\":\"x\"}");
        baixa.ErrorMessage.Should().Be("X");
        baixa.ErrorStatusCode.Should().Be(400);
    }

    [Fact]
    public void AplicarWebhookErro_FromOutroEstado_DeveLancar()
    {
        var baixa = CriarValida();

        Action act = () => baixa.AplicarWebhookErro("{}", "x", null);

        act.Should().Throw<InvalidOperationException>();
    }

    // ---------------------------------------------------------------------
    // MarcarFalhaEnvio
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(SerasaPefinBaixaStatus.Aprovada)]
    [InlineData(SerasaPefinBaixaStatus.PendenteEnvio)]
    public void MarcarFalhaEnvio_FromEstadosValidos_TransicionaParaAprovadaFalhaEnvio(SerasaPefinBaixaStatus origem)
    {
        var baixa = CriarValida();
        baixa.MarcarAprovada("aprovador", DateTime.UtcNow);
        if (origem == SerasaPefinBaixaStatus.PendenteEnvio)
        {
            baixa.MarcarPendenteEnvio();
        }

        baixa.MarcarFalhaEnvio("timeout", 504);

        baixa.Status.Should().Be(SerasaPefinBaixaStatus.AprovadaFalhaEnvio);
        baixa.ErrorMessage.Should().Be("timeout");
        baixa.ErrorStatusCode.Should().Be(504);
    }

    [Fact]
    public void MarcarFalhaEnvio_FromAguardandoAprovacao_DeveLancar()
    {
        var baixa = CriarValida();

        Action act = () => baixa.MarcarFalhaEnvio("x", 500);

        act.Should().Throw<InvalidOperationException>();
    }

    // ---------------------------------------------------------------------
    // RegistrarTentativaReenvio
    // ---------------------------------------------------------------------

    [Fact]
    public void RegistrarTentativaReenvio_FromBaixadoErroComTentativasAbaixoLimite_IncrementaETransicionaParaPendenteEnvio()
    {
        var baixa = CriarValida();
        baixa.MarcarAprovada("aprovador", DateTime.UtcNow);
        baixa.MarcarPendenteEnvio();
        baixa.MarcarBaixaAguardandoRetorno("uuid-1");
        baixa.AplicarWebhookErro("{}", "x", 400);

        baixa.RegistrarTentativaReenvio();

        baixa.Status.Should().Be(SerasaPefinBaixaStatus.PendenteEnvio);
        baixa.Tentativas.Should().Be(2);
        // limpa erros para a nova tentativa
        baixa.ErrorMessage.Should().BeNull();
        baixa.ErrorStatusCode.Should().BeNull();
        baixa.TransactionId.Should().BeNull();
    }

    [Fact]
    public void RegistrarTentativaReenvio_FromEstadoNaoBaixadoErro_DeveLancar()
    {
        var baixa = CriarValida();

        Action act = () => baixa.RegistrarTentativaReenvio();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RegistrarTentativaReenvio_AoAtingirLimiteDeTresTentativas_DeveLancar()
    {
        var baixa = CriarValida();
        baixa.MarcarAprovada("aprovador", DateTime.UtcNow);

        // Tentativa 1 -> erro -> reenvio (Tentativas vira 2)
        baixa.MarcarPendenteEnvio();
        baixa.MarcarBaixaAguardandoRetorno("uuid-1");
        baixa.AplicarWebhookErro("{}", "x", 400);
        baixa.RegistrarTentativaReenvio();

        // Tentativa 2 -> erro -> reenvio (Tentativas vira 3)
        baixa.MarcarBaixaAguardandoRetorno("uuid-2");
        baixa.AplicarWebhookErro("{}", "x", 400);
        baixa.RegistrarTentativaReenvio();

        baixa.Tentativas.Should().Be(3);

        // Tentativa 3 -> erro -> 4ª tentativa proibida
        baixa.MarcarBaixaAguardandoRetorno("uuid-3");
        baixa.AplicarWebhookErro("{}", "x", 400);
        Action act = () => baixa.RegistrarTentativaReenvio();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*limite*");
    }

    // ---------------------------------------------------------------------
    // Hydrate
    // ---------------------------------------------------------------------

    [Fact]
    public void Hydrate_ReconstroiAggregateSemRodarFactoryInvariants()
    {
        var id = Guid.NewGuid();
        var idNeg = Guid.NewGuid();
        var dt = DateTime.UtcNow;

        var baixa = SerasaPefinBaixaSolicitacao.Hydrate(
            id: id,
            idSolicitacaoNegativacao: idNeg,
            numVendaFk: 1,
            numeroParcela: 1,
            contractNumber: "1",
            documentoDevedor: "12345678901",
            documentoCredor: "98765432100123",
            motivo: SerasaPefinBaixaMotivo.From(3),
            status: SerasaPefinBaixaStatus.BaixadoSucesso,
            solicitanteUsername: "solicitante",
            aprovadorUsername: "aprovador",
            dtAprovacao: dt,
            justificativa: null,
            transactionId: "uuid-1",
            webhookPayload: "{}",
            errorMessage: null,
            errorStatusCode: null,
            tentativas: 1,
            dtCriacao: dt,
            dtAtualizacao: dt);

        baixa.Id.Should().Be(id);
        baixa.IdSolicitacaoNegativacao.Should().Be(idNeg);
        baixa.Status.Should().Be(SerasaPefinBaixaStatus.BaixadoSucesso);
        baixa.AprovadorUsername.Should().Be("aprovador");
        baixa.TransactionId.Should().Be("uuid-1");
    }
}
