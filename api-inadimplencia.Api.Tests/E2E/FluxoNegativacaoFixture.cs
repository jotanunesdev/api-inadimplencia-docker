using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using ApiInadimplencia.Application.Abstractions.Auth;
using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Negativacao.Commands;
using ApiInadimplencia.Application.Features.SerasaPefin.Commands;
using ApiInadimplencia.Application.Features.SerasaPefin.Dtos;
using ApiInadimplencia.Domain.SerasaPefin;
using api_inadimplencia.Api.Tests.Infrastructure;

namespace api_inadimplencia.Api.Tests.E2E;

/// <summary>
/// Fixture for E2E tests of the negativacao fluxo.
/// Configures WebApplicationFactory with mocked dependencies.
/// </summary>
public class FluxoNegativacaoFixture : ApiTestWebApplicationFactory
{
    public Mock<ICurrentUserService> CurrentUserServiceMock { get; } = new();
    public Mock<ICommandHandler<RequestNegativacaoCommand, RequestNegativacaoResponse>> RequestNegativacaoMock { get; } = new();

    public FluxoNegativacaoFixture()
    {
        ConfigureRequestNegativacaoSuccess();
    }

    public override async Task ResetStateAsync()
    {
        await base.ResetStateAsync();

        CurrentUserServiceMock.Reset();
        RequestNegativacaoMock.Reset();
        SetCurrentUser(string.Empty, false);
        ConfigureRequestNegativacaoSuccess();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            // Replace ICurrentUserService with mock
            services.RemoveAll<ICurrentUserService>();
            services.AddScoped(_ => CurrentUserServiceMock.Object);

            // Replace RequestNegativacaoFluxoCommand handler with mock
            services.RemoveAll<ICommandHandler<RequestNegativacaoCommand, RequestNegativacaoResponse>>();
            services.AddScoped(_ => RequestNegativacaoMock.Object);
        });
    }

    /// <summary>
    /// Sets the current user context for the test.
    /// </summary>
    public void SetCurrentUser(string username, bool isAuthenticated = true)
    {
        if (isAuthenticated)
        {
            CurrentUserServiceMock.Setup(x => x.Username).Returns(username);
            CurrentUserServiceMock.Setup(x => x.IsAuthenticated).Returns(true);
        }
        else
        {
            CurrentUserServiceMock.Setup(x => x.Username).Returns((string?)null);
            CurrentUserServiceMock.Setup(x => x.IsAuthenticated).Returns(false);
        }
    }

    /// <summary>
    /// Configures the RequestNegativacaoFluxoCommand handler mock to return a successful response.
    /// </summary>
    public void ConfigureRequestNegativacaoSuccess(Guid? solicitacaoId = null)
    {
        RequestNegativacaoMock
            .Setup(x => x.HandleAsync(It.IsAny<RequestNegativacaoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RequestNegativacaoResponse(
                [
                    new SerasaSolicitacaoResult(
                        solicitacaoId ?? Guid.NewGuid(),
                        SerasaPefinRecordType.Principal,
                        "tx-test-123",
                        SerasaPefinStatus.AguardandoRetorno,
                        null,
                        1)
                ],
                SerasaPefinStatus.AguardandoRetorno));
    }

    /// <summary>
    /// Configures the RequestNegativacaoFluxoCommand handler mock to return an error.
    /// </summary>
    public void ConfigureRequestNegativacaoError(string errorMessage = "Request error")
    {
        RequestNegativacaoMock
            .Setup(x => x.HandleAsync(It.IsAny<RequestNegativacaoCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException(errorMessage));
    }
}
