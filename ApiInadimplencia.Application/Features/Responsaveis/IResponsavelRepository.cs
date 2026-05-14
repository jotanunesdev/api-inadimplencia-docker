using ApiInadimplencia.Domain.Responsaveis;

namespace ApiInadimplencia.Application.Features.Responsaveis;

/// <summary>
/// Repository interface for responsible user assignments.
/// </summary>
public interface IResponsavelRepository
{
    Task<VendaResponsavel?> GetByNumVendaAsync(int numVenda, CancellationToken cancellationToken);
    Task UpsertAsync(VendaResponsavel responsavel, CancellationToken cancellationToken);
    Task DeleteAsync(int numVenda, CancellationToken cancellationToken);
}
