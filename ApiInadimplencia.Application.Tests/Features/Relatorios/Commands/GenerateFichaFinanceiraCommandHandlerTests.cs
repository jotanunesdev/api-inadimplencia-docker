using ApiInadimplencia.Application.Abstractions.Integrations;
using ApiInadimplencia.Application.Configuration;
using ApiInadimplencia.Application.Features.Relatorios.Commands;
using ApiInadimplencia.Application.Features.Relatorios.Dtos;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ApiInadimplencia.Application.Tests.Features.Relatorios.Commands;

public class GenerateFichaFinanceiraCommandHandlerTests
{
    private static readonly string SampleParamsXml = """
        <ArrayOfRptParameterReportPar>
          <RptParameterReportPar>
            <ParamName>CODCOLIGADA</ParamName>
            <Value>0</Value>
          </RptParameterReportPar>
          <RptParameterReportPar>
            <ParamName>NUMVENDA</ParamName>
            <Value>0</Value>
          </RptParameterReportPar>
          <RptParameterReportPar>
            <ParamName>OUTRO</ParamName>
            <Value>preserve</Value>
          </RptParameterReportPar>
        </ArrayOfRptParameterReportPar>
        """;

    private static IOptions<RmOptions> Options(RmOptions opts) => Microsoft.Extensions.Options.Options.Create(opts);

    private static RmOptions Defaults() => new()
    {
        Coligada = 1,
        ReportColigada = 0,
        ParamColigada = 1,
        ReportId = 21968,
        ReportCode = "21968",
        ReportName = "Ficha Financeira",
    };

    private static FluigDatasetResponse ParamsResponse(string xml) =>
        new(new[]
        {
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { ["RESULTADO"] = xml },
        });

    private static FluigDatasetResponse ErrorResponse(string error) =>
        new(new[]
        {
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { ["ERRO"] = error },
        });

    private static FluigDatasetResponse UrlResponse(string url) =>
        new(new[]
        {
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { ["RETORNO"] = url },
        });

    [Fact]
    public async Task HandleAsync_ShouldReturnUrl_AndApplyParameterSubstitution()
    {
        var gateway = new Mock<IFluigDatasetGateway>(MockBehavior.Strict);

        gateway
            .Setup(g => g.SearchAsync(It.Is<FluigDatasetRequest>(r => r.DatasetName == "ds_paramsRel"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ParamsResponse(SampleParamsXml));

        FluigDatasetRequest? captured = null;
        gateway
            .Setup(g => g.SearchAsync(It.Is<FluigDatasetRequest>(r => r.DatasetName == "dsIntegraFacilRM"), It.IsAny<CancellationToken>()))
            .Callback<FluigDatasetRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(UrlResponse("https://rm.example/Report.pdf"));

        var handler = new GenerateFichaFinanceiraCommandHandler(gateway.Object, Options(Defaults()), NullLogger<GenerateFichaFinanceiraCommandHandler>.Instance);

        var url = await handler.HandleAsync(new GenerateFichaFinanceiraCommand(NumVenda: 12345));

        url.Should().Be("https://rm.example/Report.pdf");
        captured.Should().NotBeNull();
        var parameter = captured!.Constraints!.Single(c => c.Field == "PARAMETER");
        // Defaults().ParamColigada = 1, NumVenda = 12345.
        parameter.InitialValue.Should().Contain("<Value>1</Value>");
        parameter.InitialValue.Should().Contain("<Value>12345</Value>");
        // Untouched parameter must be preserved.
        parameter.InitialValue.Should().Contain("<Value>preserve</Value>");
    }

    [Fact]
    public async Task HandleAsync_ShouldFallbackToAlternateColigada_WhenReportNotFound()
    {
        var gateway = new Mock<IFluigDatasetGateway>(MockBehavior.Strict);
        var calls = new List<FluigDatasetRequest>();

        gateway
            .Setup(g => g.SearchAsync(It.Is<FluigDatasetRequest>(r => r.DatasetName == "ds_paramsRel"), It.IsAny<CancellationToken>()))
            .Callback<FluigDatasetRequest, CancellationToken>((r, _) => calls.Add(r))
            .ReturnsAsync((FluigDatasetRequest r, CancellationToken _) =>
            {
                // First attempt: configured ReportColigada=0 → not found.
                // After meta lookup we still don't match → swap to 1 → success.
                var coligada = r.Fields![0];
                return coligada == "1"
                    ? ParamsResponse(SampleParamsXml)
                    : ErrorResponse("Relatorio nao localizado.");
            });

        gateway
            .Setup(g => g.SearchAsync(It.Is<FluigDatasetRequest>(r => r.DatasetName == "ds_paiFilho_controleDeAcessoRMreportsFluig"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluigDatasetResponse(Array.Empty<IReadOnlyDictionary<string, string?>>()));

        gateway
            .Setup(g => g.SearchAsync(It.Is<FluigDatasetRequest>(r => r.DatasetName == "dsIntegraFacilRM"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UrlResponse("https://rm.example/Report.pdf"));

        var handler = new GenerateFichaFinanceiraCommandHandler(gateway.Object, Options(Defaults()), NullLogger<GenerateFichaFinanceiraCommandHandler>.Instance);

        var url = await handler.HandleAsync(new GenerateFichaFinanceiraCommand(NumVenda: 555));

        url.Should().Be("https://rm.example/Report.pdf");
        calls.Should().HaveCount(2);
        calls[0].Fields![0].Should().Be("0"); // configured
        calls[1].Fields![0].Should().Be("1"); // alternate after swap
    }

    [Fact]
    public async Task HandleAsync_ShouldThrow_WhenAllAttemptsFail()
    {
        var gateway = new Mock<IFluigDatasetGateway>();
        gateway
            .Setup(g => g.SearchAsync(It.Is<FluigDatasetRequest>(r => r.DatasetName == "ds_paramsRel"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ErrorResponse("Relatorio nao localizado."));
        gateway
            .Setup(g => g.SearchAsync(It.Is<FluigDatasetRequest>(r => r.DatasetName == "ds_paiFilho_controleDeAcessoRMreportsFluig"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluigDatasetResponse(Array.Empty<IReadOnlyDictionary<string, string?>>()));

        var handler = new GenerateFichaFinanceiraCommandHandler(gateway.Object, Options(Defaults()), NullLogger<GenerateFichaFinanceiraCommandHandler>.Instance);

        var act = () => handler.HandleAsync(new GenerateFichaFinanceiraCommand(NumVenda: 1));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*não localizado*");
    }

    [Fact]
    public async Task HandleAsync_ShouldThrow_WhenNumVendaInvalid()
    {
        var gateway = new Mock<IFluigDatasetGateway>();
        var handler = new GenerateFichaFinanceiraCommandHandler(gateway.Object, Options(Defaults()), NullLogger<GenerateFichaFinanceiraCommandHandler>.Instance);

        var act = () => handler.HandleAsync(new GenerateFichaFinanceiraCommand(NumVenda: 0));

        await act.Should().ThrowAsync<ArgumentException>();
    }

}
