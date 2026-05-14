using ApiInadimplencia.Application.Abstractions.Cqrs;

namespace ApiInadimplencia.Application.Features.Atendimentos.Dtos;

/// <summary>
/// Command to create a new attendance record.
/// </summary>
/// <param name="Cpf">Customer CPF.</param>
/// <param name="NumVendaFk">Sale number.</param>
/// <param name="DadosVenda">Sale data object to be serialized as JSON.</param>
public record CreateAtendimentoCommand(
    string Cpf,
    int NumVendaFk,
    object DadosVenda) : ICommand<string>;
