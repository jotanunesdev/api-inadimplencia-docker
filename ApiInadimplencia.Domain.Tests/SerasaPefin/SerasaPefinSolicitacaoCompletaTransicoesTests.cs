using ApiInadimplencia.Domain.SerasaPefin;
using Xunit;

namespace ApiInadimplencia.Domain.Tests.SerasaPefin;

public sealed class SerasaPefinSolicitacaoCompletaTransicoesTests
{
    [Fact]
    public void CriarParaAprovacao_CriaComStatusCorretoESemTransactionId()
    {
        // Arrange
        var numVendaFk = 12345;
        var tipoRegistro = SerasaPefinRecordType.Principal;
        var documentoDevedor = "12345678901";
        var documentoCredor = "98765432100123";
        var contractNumber = "12345";
        var areaInformante = "1234";
        var valor = 1000.50m;
        var dataVencimento = new DateOnly(2025, 12, 31);
        var solicitanteUsername = "solicitante";

        // Act
        var result = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk,
            tipoRegistro,
            documentoDevedor,
            documentoCredor,
            contractNumber,
            areaInformante,
            valor,
            dataVencimento,
            solicitanteUsername);

        // Assert
        Assert.Equal(SerasaPefinStatus.AguardandoAprovacao, result.Status);
        Assert.Null(result.TransactionId);
        Assert.Equal(solicitanteUsername, result.SolicitanteUsername);
        Assert.Null(result.AprovadorUsername);
        Assert.Null(result.DtAprovacao);
        Assert.Null(result.Justificativa);
        Assert.Equal("{}", result.PayloadAuditoria);
    }

    [Fact]
    public void MarcarAprovada_ExigeStatusAguardandoAprovacao_PreencheAprovadorUsernameEDtAprovacao()
    {
        // Arrange
        var solicitacao = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            12345,
            SerasaPefinRecordType.Principal,
            "12345678901",
            "98765432100123",
            "12345",
            "1234",
            1000.50m,
            new DateOnly(2025, 12, 31),
            "solicitante");
        var aprovadorUsername = "aprovador";
        var utcNow = DateTime.UtcNow;

        // Act
        solicitacao.MarcarAprovada(aprovadorUsername, utcNow);

        // Assert
        Assert.Equal(SerasaPefinStatus.Aprovada, solicitacao.Status);
        Assert.Equal(aprovadorUsername, solicitacao.AprovadorUsername);
        Assert.Equal(utcNow, solicitacao.DtAprovacao);
    }

    [Fact]
    public void MarcarAprovada_StatusInvalido_DeveLancarInvalidOperationException()
    {
        // Arrange
        var solicitacao = SerasaPefinSolicitacaoCompleta.Criar(
            12345,
            SerasaPefinRecordType.Principal,
            "12345678901",
            "98765432100123",
            "12345",
            "1234",
            1000.50m,
            new DateOnly(2025, 12, 31),
            "operador",
            "{}");
        solicitacao.MarcarAguardandoRetorno("txn123");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => solicitacao.MarcarAprovada("aprovador", DateTime.UtcNow));
    }

    [Fact]
    public void MarcarRejeitada_ExigeStatusAguardandoAprovacao_PreencheJustificativa()
    {
        // Arrange
        var solicitacao = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            12345,
            SerasaPefinRecordType.Principal,
            "12345678901",
            "98765432100123",
            "12345",
            "1234",
            1000.50m,
            new DateOnly(2025, 12, 31),
            "solicitante");
        var aprovadorUsername = "aprovador";
        var justificativa = "Dados inconsistentes";
        var utcNow = DateTime.UtcNow;

        // Act
        solicitacao.MarcarRejeitada(aprovadorUsername, justificativa, utcNow);

        // Assert
        Assert.Equal(SerasaPefinStatus.Rejeitada, solicitacao.Status);
        Assert.Equal(aprovadorUsername, solicitacao.AprovadorUsername);
        Assert.Equal(utcNow, solicitacao.DtAprovacao);
        Assert.Equal(justificativa, solicitacao.Justificativa);
    }

    [Fact]
    public void MarcarRejeitada_StatusInvalido_DeveLancarInvalidOperationException()
    {
        // Arrange
        var solicitacao = SerasaPefinSolicitacaoCompleta.Criar(
            12345,
            SerasaPefinRecordType.Principal,
            "12345678901",
            "98765432100123",
            "12345",
            "1234",
            1000.50m,
            new DateOnly(2025, 12, 31),
            "operador",
            "{}");
        solicitacao.MarcarAguardandoRetorno("txn123");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => solicitacao.MarcarRejeitada("aprovador", "justificativa", DateTime.UtcNow));
    }

    [Fact]
    public void MarcarPreparadoParaEnvio_ExigeStatusAprovada()
    {
        // Arrange
        var solicitacao = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            12345,
            SerasaPefinRecordType.Principal,
            "12345678901",
            "98765432100123",
            "12345",
            "1234",
            1000.50m,
            new DateOnly(2025, 12, 31),
            "solicitante");
        solicitacao.MarcarAprovada("aprovador", DateTime.UtcNow);
        var payloadAuditoria = "{\"masked\": true}";

        // Act
        solicitacao.MarcarPreparadoParaEnvio(payloadAuditoria);

        // Assert
        Assert.Equal(SerasaPefinStatus.PendenteEnvio, solicitacao.Status);
        Assert.Equal(payloadAuditoria, solicitacao.PayloadAuditoria);
    }

    [Fact]
    public void MarcarPreparadoParaEnvio_StatusInvalido_DeveLancarInvalidOperationException()
    {
        // Arrange
        var solicitacao = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            12345,
            SerasaPefinRecordType.Principal,
            "12345678901",
            "98765432100123",
            "12345",
            "1234",
            1000.50m,
            new DateOnly(2025, 12, 31),
            "solicitante");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => solicitacao.MarcarPreparadoParaEnvio("{\"masked\": true}"));
    }

    [Fact]
    public void MarcarAprovadaFalhaEnvio_ExigeStatusAprovadaOuPendenteEnvio()
    {
        // Arrange
        var solicitacao = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            12345,
            SerasaPefinRecordType.Principal,
            "12345678901",
            "98765432100123",
            "12345",
            "1234",
            1000.50m,
            new DateOnly(2025, 12, 31),
            "solicitante");
        solicitacao.MarcarAprovada("aprovador", DateTime.UtcNow);
        var errorMessage = "Erro de conexão";
        var statusCode = 500;

        // Act
        solicitacao.MarcarAprovadaFalhaEnvio(errorMessage, statusCode);

        // Assert
        Assert.Equal(SerasaPefinStatus.AprovadaFalhaEnvio, solicitacao.Status);
        Assert.Equal(errorMessage, solicitacao.ErrorMessage);
        Assert.Equal(statusCode, solicitacao.ErrorStatusCode);
    }

    [Fact]
    public void MarcarAprovadaFalhaEnvio_DePendenteEnvio()
    {
        // Arrange
        var solicitacao = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            12345,
            SerasaPefinRecordType.Principal,
            "12345678901",
            "98765432100123",
            "12345",
            "1234",
            1000.50m,
            new DateOnly(2025, 12, 31),
            "solicitante");
        solicitacao.MarcarAprovada("aprovador", DateTime.UtcNow);
        solicitacao.MarcarPreparadoParaEnvio("{\"masked\": true}");
        var errorMessage = "Erro de conexão";
        var statusCode = 500;

        // Act
        solicitacao.MarcarAprovadaFalhaEnvio(errorMessage, statusCode);

        // Assert
        Assert.Equal(SerasaPefinStatus.AprovadaFalhaEnvio, solicitacao.Status);
        Assert.Equal(errorMessage, solicitacao.ErrorMessage);
        Assert.Equal(statusCode, solicitacao.ErrorStatusCode);
    }

    [Fact]
    public void MarcarAprovadaFalhaEnvio_StatusInvalido_DeveLancarInvalidOperationException()
    {
        // Arrange
        var solicitacao = SerasaPefinSolicitacaoCompleta.Criar(
            12345,
            SerasaPefinRecordType.Principal,
            "12345678901",
            "98765432100123",
            "12345",
            "1234",
            1000.50m,
            new DateOnly(2025, 12, 31),
            "operador",
            "{}");
        solicitacao.MarcarAguardandoRetorno("txn123");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => solicitacao.MarcarAprovadaFalhaEnvio("erro", 500));
    }

    [Fact]
    public void Transicao_AguardandoAprovacao_Para_PendenteEnvio_Direto_DeveLancarInvalidOperationException()
    {
        // Arrange
        var solicitacao = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            12345,
            SerasaPefinRecordType.Principal,
            "12345678901",
            "98765432100123",
            "12345",
            "1234",
            1000.50m,
            new DateOnly(2025, 12, 31),
            "solicitante");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => solicitacao.MarcarPreparadoParaEnvio("{\"masked\": true}"));
    }

    [Fact]
    public void Criar_SemParcela_CriaSolicitacaoLegada_CompatibilidadeRetroativa()
    {
        // Arrange
        var numVendaFk = 12345;
        var tipoRegistro = SerasaPefinRecordType.Principal;
        var documentoDevedor = "12345678901";
        var documentoCredor = "98765432100123";
        var contractNumber = "12345";
        var areaInformante = "1234";
        var valor = 1000.50m;
        var dataVencimento = new DateOnly(2025, 12, 31);
        var operador = "operador";
        var payloadAuditoria = "{}";

        // Act
        var result = SerasaPefinSolicitacaoCompleta.Criar(
            numVendaFk,
            tipoRegistro,
            documentoDevedor,
            documentoCredor,
            contractNumber,
            areaInformante,
            valor,
            dataVencimento,
            operador,
            payloadAuditoria);

        // Assert
        Assert.Null(result.NumeroParcela);
        Assert.Null(result.ParcelaIdOrigem);
        Assert.Null(result.IdSolicitacaoPai);
    }

    [Fact]
    public void Criar_ComParcela_CriaSolicitacaoComDadosDeParcela()
    {
        // Arrange
        var numVendaFk = 12345;
        var tipoRegistro = SerasaPefinRecordType.Principal;
        var documentoDevedor = "12345678901";
        var documentoCredor = "98765432100123";
        var contractNumber = "12345";
        var areaInformante = "1234";
        var valor = 250.50m;
        var dataVencimento = new DateOnly(2025, 12, 31);
        var operador = "operador";
        var payloadAuditoria = "{}";
        var numeroParcela = 1;
        var parcelaIdOrigem = "TIT-12345";
        var idSolicitacaoPai = Guid.NewGuid();

        // Act
        var result = SerasaPefinSolicitacaoCompleta.Criar(
            numVendaFk,
            tipoRegistro,
            documentoDevedor,
            documentoCredor,
            contractNumber,
            areaInformante,
            valor,
            dataVencimento,
            operador,
            payloadAuditoria,
            numeroParcela: numeroParcela,
            parcelaIdOrigem: parcelaIdOrigem,
            idSolicitacaoPai: idSolicitacaoPai);

        // Assert
        Assert.Equal(numeroParcela, result.NumeroParcela);
        Assert.Equal(parcelaIdOrigem, result.ParcelaIdOrigem);
        Assert.Equal(idSolicitacaoPai, result.IdSolicitacaoPai);
    }

    [Fact]
    public void Criar_NumeroParcelaSemParcelaIdOrigem_DeveLancarArgumentException()
    {
        // Arrange
        var numVendaFk = 12345;
        var tipoRegistro = SerasaPefinRecordType.Principal;
        var documentoDevedor = "12345678901";
        var documentoCredor = "98765432100123";
        var contractNumber = "12345";
        var areaInformante = "1234";
        var valor = 1000.50m;
        var dataVencimento = new DateOnly(2025, 12, 31);
        var operador = "operador";
        var payloadAuditoria = "{}";
        var numeroParcela = 1;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => SerasaPefinSolicitacaoCompleta.Criar(
            numVendaFk,
            tipoRegistro,
            documentoDevedor,
            documentoCredor,
            contractNumber,
            areaInformante,
            valor,
            dataVencimento,
            operador,
            payloadAuditoria,
            numeroParcela: numeroParcela,
            parcelaIdOrigem: null));
    }

    [Fact]
    public void Criar_NumeroParcelaZero_DeveLancarArgumentOutOfRangeException()
    {
        // Arrange
        var numVendaFk = 12345;
        var tipoRegistro = SerasaPefinRecordType.Principal;
        var documentoDevedor = "12345678901";
        var documentoCredor = "98765432100123";
        var contractNumber = "12345";
        var areaInformante = "1234";
        var valor = 1000.50m;
        var dataVencimento = new DateOnly(2025, 12, 31);
        var operador = "operador";
        var payloadAuditoria = "{}";
        var numeroParcela = 0;
        var parcelaIdOrigem = "TIT-12345";

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => SerasaPefinSolicitacaoCompleta.Criar(
            numVendaFk,
            tipoRegistro,
            documentoDevedor,
            documentoCredor,
            contractNumber,
            areaInformante,
            valor,
            dataVencimento,
            operador,
            payloadAuditoria,
            numeroParcela: numeroParcela,
            parcelaIdOrigem: parcelaIdOrigem));
    }

    [Fact]
    public void Criar_NumeroParcelaNegativo_DeveLancarArgumentOutOfRangeException()
    {
        // Arrange
        var numVendaFk = 12345;
        var tipoRegistro = SerasaPefinRecordType.Principal;
        var documentoDevedor = "12345678901";
        var documentoCredor = "98765432100123";
        var contractNumber = "12345";
        var areaInformante = "1234";
        var valor = 1000.50m;
        var dataVencimento = new DateOnly(2025, 12, 31);
        var operador = "operador";
        var payloadAuditoria = "{}";
        var numeroParcela = -1;
        var parcelaIdOrigem = "TIT-12345";

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => SerasaPefinSolicitacaoCompleta.Criar(
            numVendaFk,
            tipoRegistro,
            documentoDevedor,
            documentoCredor,
            contractNumber,
            areaInformante,
            valor,
            dataVencimento,
            operador,
            payloadAuditoria,
            numeroParcela: numeroParcela,
            parcelaIdOrigem: parcelaIdOrigem));
    }

    [Fact]
    public void CriarParaAprovacao_SemParcela_CriaSolicitacaoLegada_CompatibilidadeRetroativa()
    {
        // Arrange
        var numVendaFk = 12345;
        var tipoRegistro = SerasaPefinRecordType.Principal;
        var documentoDevedor = "12345678901";
        var documentoCredor = "98765432100123";
        var contractNumber = "12345";
        var areaInformante = "1234";
        var valor = 1000.50m;
        var dataVencimento = new DateOnly(2025, 12, 31);
        var solicitanteUsername = "solicitante";

        // Act
        var result = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk,
            tipoRegistro,
            documentoDevedor,
            documentoCredor,
            contractNumber,
            areaInformante,
            valor,
            dataVencimento,
            solicitanteUsername);

        // Assert
        Assert.Null(result.NumeroParcela);
        Assert.Null(result.ParcelaIdOrigem);
        Assert.Null(result.IdSolicitacaoPai);
    }

    [Fact]
    public void CriarParaAprovacao_ComParcela_CriaSolicitacaoComDadosDeParcela()
    {
        // Arrange
        var numVendaFk = 12345;
        var tipoRegistro = SerasaPefinRecordType.Principal;
        var documentoDevedor = "12345678901";
        var documentoCredor = "98765432100123";
        var contractNumber = "12345";
        var areaInformante = "1234";
        var valor = 250.50m;
        var dataVencimento = new DateOnly(2025, 12, 31);
        var solicitanteUsername = "solicitante";
        var numeroParcela = 1;
        var parcelaIdOrigem = "TIT-12345";
        var idSolicitacaoPai = Guid.NewGuid();

        // Act
        var result = SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk,
            tipoRegistro,
            documentoDevedor,
            documentoCredor,
            contractNumber,
            areaInformante,
            valor,
            dataVencimento,
            solicitanteUsername,
            numeroParcela: numeroParcela,
            parcelaIdOrigem: parcelaIdOrigem,
            idSolicitacaoPai: idSolicitacaoPai);

        // Assert
        Assert.Equal(numeroParcela, result.NumeroParcela);
        Assert.Equal(parcelaIdOrigem, result.ParcelaIdOrigem);
        Assert.Equal(idSolicitacaoPai, result.IdSolicitacaoPai);
    }

    [Fact]
    public void CriarParaAprovacao_NumeroParcelaSemParcelaIdOrigem_DeveLancarArgumentException()
    {
        // Arrange
        var numVendaFk = 12345;
        var tipoRegistro = SerasaPefinRecordType.Principal;
        var documentoDevedor = "12345678901";
        var documentoCredor = "98765432100123";
        var contractNumber = "12345";
        var areaInformante = "1234";
        var valor = 1000.50m;
        var dataVencimento = new DateOnly(2025, 12, 31);
        var solicitanteUsername = "solicitante";
        var numeroParcela = 1;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => SerasaPefinSolicitacaoCompleta.CriarParaAprovacao(
            numVendaFk,
            tipoRegistro,
            documentoDevedor,
            documentoCredor,
            contractNumber,
            areaInformante,
            valor,
            dataVencimento,
            solicitanteUsername,
            numeroParcela: numeroParcela,
            parcelaIdOrigem: null));
    }

    [Fact]
    public void Hydrate_ComParcela_RestauraDadosDeParcela()
    {
        // Arrange
        var id = Guid.NewGuid();
        var numVendaFk = 12345;
        var tipoRegistro = SerasaPefinRecordType.Principal;
        var documentoDevedor = "12345678901";
        var documentoCredor = "98765432100123";
        var contractNumber = "12345";
        var areaInformante = "1234";
        var valor = 250.50m;
        var dataVencimento = new DateOnly(2025, 12, 31);
        var status = SerasaPefinStatus.PendenteEnvio;
        var operador = "operador";
        var dtCriacao = DateTime.UtcNow;
        var dtAtualizacao = DateTime.UtcNow;
        var numeroParcela = 1;
        var parcelaIdOrigem = "TIT-12345";
        var idSolicitacaoPai = Guid.NewGuid();

        // Act
        var result = SerasaPefinSolicitacaoCompleta.Hydrate(
            id,
            numVendaFk,
            tipoRegistro,
            null,
            null,
            null,
            documentoDevedor,
            null,
            documentoCredor,
            contractNumber,
            SerasaPefinConstants.CategoryId,
            areaInformante,
            valor,
            dataVencimento,
            status,
            null,
            null,
            null,
            "{}",
            null,
            null,
            null,
            operador,
            dtCriacao,
            dtAtualizacao,
            numeroParcela: numeroParcela,
            parcelaIdOrigem: parcelaIdOrigem,
            idSolicitacaoPai: idSolicitacaoPai);

        // Assert
        Assert.Equal(numeroParcela, result.NumeroParcela);
        Assert.Equal(parcelaIdOrigem, result.ParcelaIdOrigem);
        Assert.Equal(idSolicitacaoPai, result.IdSolicitacaoPai);
    }

    [Fact]
    public void Hydrate_SemParcela_RestauraSolicitacaoLegada()
    {
        // Arrange
        var id = Guid.NewGuid();
        var numVendaFk = 12345;
        var tipoRegistro = SerasaPefinRecordType.Principal;
        var documentoDevedor = "12345678901";
        var documentoCredor = "98765432100123";
        var contractNumber = "12345";
        var areaInformante = "1234";
        var valor = 1000.50m;
        var dataVencimento = new DateOnly(2025, 12, 31);
        var status = SerasaPefinStatus.PendenteEnvio;
        var operador = "operador";
        var dtCriacao = DateTime.UtcNow;
        var dtAtualizacao = DateTime.UtcNow;

        // Act
        var result = SerasaPefinSolicitacaoCompleta.Hydrate(
            id,
            numVendaFk,
            tipoRegistro,
            null,
            null,
            null,
            documentoDevedor,
            null,
            documentoCredor,
            contractNumber,
            SerasaPefinConstants.CategoryId,
            areaInformante,
            valor,
            dataVencimento,
            status,
            null,
            null,
            null,
            "{}",
            null,
            null,
            null,
            operador,
            dtCriacao,
            dtAtualizacao);

        // Assert
        Assert.Null(result.NumeroParcela);
        Assert.Null(result.ParcelaIdOrigem);
        Assert.Null(result.IdSolicitacaoPai);
    }
}
