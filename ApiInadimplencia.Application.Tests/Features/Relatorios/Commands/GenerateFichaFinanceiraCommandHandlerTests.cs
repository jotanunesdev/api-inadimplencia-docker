// This test is disabled because GenerateFichaFinanceiraCommandHandler is disabled (.disabled file)
// Re-enable when the handler is re-enabled
// using ApiInadimplencia.Application.Abstractions.Integrations;
// using ApiInadimplencia.Application.Features.Relatorios.Commands;
// using ApiInadimplencia.Application.Features.Relatorios.Dtos;
// using Microsoft.Extensions.Logging;
// using Moq;
// using Xunit;

// namespace ApiInadimplencia.Application.Tests.Features.Relatorios.Commands;

// public class GenerateFichaFinanceiraCommandHandlerTests
// {
//     private readonly Mock<IFluigDatasetGateway> _fluigGatewayMock;
//     private readonly Mock<IRmReportGateway> _rmGatewayMock;
//     private readonly Mock<ILogger<GenerateFichaFinanceiraCommandHandler>> _loggerMock;
//     private readonly GenerateFichaFinanceiraCommandHandler _handler;

//     public GenerateFichaFinanceiraCommandHandlerTests()
//     {
//         _fluigGatewayMock = new Mock<IFluigDatasetGateway>();
//         _rmGatewayMock = new Mock<IRmReportGateway>();
//         _loggerMock = new Mock<ILogger<GenerateFichaFinanceiraCommandHandler>>();
//         _handler = new GenerateFichaFinanceiraCommandHandler(
//             _fluigGatewayMock.Object,
//             _rmGatewayMock.Object,
//             _loggerMock.Object);
//     }

//     [Fact]
//     public async Task Handle_ShouldReturnPdfUrl_WhenSuccessful()
//     {
//         // Arrange
//         var command = new GenerateFichaFinanceiraCommand(
//             NumVenda: 12345,
//             CodColigada: "1",
//             ReportColigada: "1",
//             ReportId: "ficha-financeira");

//         var xmlParameters = "<parameters><COLIGADA>1</COLIGADA><NUMVENDA>12345</NUMVENDA></parameters>";
//         var expectedUrl = "https://rm.example.com/reports/Report.pdf";

//         _fluigGatewayMock
//             .Setup(x => x.GetDatasetAsync("ds_paramsRel", It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
//             .ReturnsAsync(xmlParameters);

//         _rmGatewayMock
//             .Setup(x => x.GenerateReportAsync(command.ReportId, It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
//             .ReturnsAsync(expectedUrl);

//         // Act
//         var result = await _handler.Handle(command, CancellationToken.None);

//         // Assert
//         Assert.Equal(expectedUrl, result);
//         _fluigGatewayMock.Verify(x => x.GetDatasetAsync("ds_paramsRel", It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Once);
//         _rmGatewayMock.Verify(x => x.GenerateReportAsync(command.ReportId, It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Once);
//     }

//     [Fact]
//     public async Task Handle_ShouldUseFallbackDataset_WhenPrimaryFails()
//     {
//         // Arrange
//         var command = new GenerateFichaFinanceiraCommand(
//             NumVenda: 12345,
//             CodColigada: "1",
//             ReportColigada: "1",
//             ReportId: "ficha-financeira");

//         var xmlParameters = "<parameters><COLIGADA>1</COLIGADA><NUMVENDA>12345</NUMVENDA></parameters>";
//         var expectedUrl = "https://rm.example.com/reports/Report.pdf";

//         _fluigGatewayMock
//             .SetupSequence(x => x.GetDatasetAsync("ds_paramsRel", It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
//             .ThrowsAsync(new Exception("Primary dataset failed"))
//             .ReturnsAsync(xmlParameters);

//         _rmGatewayMock
//             .Setup(x => x.GenerateReportAsync(command.ReportId, It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
//             .ReturnsAsync(expectedUrl);

//         // Act
//         var result = await _handler.Handle(command, CancellationToken.None);

//         // Assert
//         Assert.Equal(expectedUrl, result);
//         _fluigGatewayMock.Verify(x => x.GetDatasetAsync("ds_paramsRel", It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
//         _fluigGatewayMock.Verify(x => x.GetDatasetAsync("ds_paiFilho_controleDeAcessoRMreportsFluig", It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Once);
//     }

//     [Fact]
//     public async Task Handle_ShouldReplaceParametersInXml()
//     {
//         // Arrange
//         var command = new GenerateFichaFinanceiraCommand(
//             NumVenda: 12345,
//             CodColigada: "1",
//             ReportColigada: "2",
//             ReportId: "ficha-financeira");

//         var xmlParameters = "<parameters><COLIGADA>1</COLIGADA><NUMVENDA>00000</NUMVENDA></parameters>";
//         var expectedUrl = "https://rm.example.com/reports/Report.pdf";

//         _fluigGatewayMock
//             .Setup(x => x.GetDatasetAsync("ds_paramsRel", It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
//             .ReturnsAsync(xmlParameters);

//         _rmGatewayMock
//             .Setup(x => x.GenerateReportAsync(command.ReportId, It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
//             .ReturnsAsync(expectedUrl);

//         // Act
//         var result = await _handler.Handle(command, CancellationToken.None);

//         // Assert
//         Assert.Equal(expectedUrl, result);
//         _rmGatewayMock.Verify(x => x.GenerateReportAsync(
//             command.ReportId, 
//             It.Is<Dictionary<string, string>>(p => 
//                 p["COLIGADA"] == "2" && 
//                 p["NUMVENDA"] == "12345"), 
//             It.IsAny<CancellationToken>()), Times.Once);
//     }
// }
