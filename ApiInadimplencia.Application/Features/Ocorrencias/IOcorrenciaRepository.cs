using ApiInadimplencia.Domain.Ocorrencias;

namespace ApiInadimplencia.Application.Features.Ocorrencias;

/// <summary>
/// Repository interface for occurrences.
/// </summary>
public interface IOcorrenciaRepository
{
    Task AddAsync(Ocorrencia ocorrencia, CancellationToken cancellationToken);
    Task<Ocorrencia?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task UpdateAsync(Ocorrencia ocorrencia, CancellationToken cancellationToken);
    Task DeleteAsync(Ocorrencia ocorrencia, CancellationToken cancellationToken);
}

/// <summary>
/// Validator for checking if a sale exists.
/// </summary>
public interface IVendaValidator
{
    Task<bool> VendaExisteAsync(int numVenda, CancellationToken cancellationToken);
}
