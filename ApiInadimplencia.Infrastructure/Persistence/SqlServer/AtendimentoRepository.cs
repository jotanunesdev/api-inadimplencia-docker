using ApiInadimplencia.Application.Abstractions.Persistence;
using ApiInadimplencia.Application.Features.Atendimentos.Commands;
using ApiInadimplencia.Domain.Atendimentos;

namespace ApiInadimplencia.Infrastructure.Persistence.SqlServer;

/// <summary>
/// SQL Server implementation of the attendance repository.
/// </summary>
public class AtendimentoRepository : IAtendimentoRepository
{
    private readonly ILegacySqlExecutor _sqlExecutor;

    public AtendimentoRepository(ILegacySqlExecutor sqlExecutor)
    {
        _sqlExecutor = sqlExecutor;
    }

    public async Task AddAsync(Atendimento atendimento, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, object?>
        {
            { "ID", atendimento.Id },
            { "PROTOCOLO", atendimento.Protocolo },
            { "CPF", atendimento.Cpf },
            { "NUM_VENDA_FK", atendimento.NumVendaFk },
            { "DADOS_VENDA_JSON", atendimento.DadosVendaJson },
            { "CRIADO_EM", atendimento.CriadoEm }
        };

        await _sqlExecutor.ExecuteAsync(
            "Atendimento.Insert",
            parameters,
            cancellationToken: cancellationToken);
    }
}
