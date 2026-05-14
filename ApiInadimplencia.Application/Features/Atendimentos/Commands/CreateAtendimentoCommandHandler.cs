using System.Text.Json;
using ApiInadimplencia.Application.Abstractions;
using ApiInadimplencia.Application.Abstractions.Cqrs;
using ApiInadimplencia.Application.Features.Atendimentos.Dtos;
using ApiInadimplencia.Domain.Atendimentos;

namespace ApiInadimplencia.Application.Features.Atendimentos.Commands;

/// <summary>
/// Handles the creation of a new attendance record with protocol generation.
/// </summary>
public class CreateAtendimentoCommandHandler : ICommandHandler<CreateAtendimentoCommand, string>
{
    private readonly IProtocoloGenerator _protocoloGenerator;
    private readonly IAtendimentoRepository _atendimentoRepository;

    public CreateAtendimentoCommandHandler(
        IProtocoloGenerator protocoloGenerator,
        IAtendimentoRepository atendimentoRepository)
    {
        _protocoloGenerator = protocoloGenerator;
        _atendimentoRepository = atendimentoRepository;
    }

    /// <inheritdoc />
    public async Task<string> HandleAsync(CreateAtendimentoCommand command, CancellationToken cancellationToken = default)
    {
        // Generate protocol
        var protocolo = await _protocoloGenerator.GerarProtocoloAsync(cancellationToken);

        // Serialize sale data to JSON
        var dadosVendaJson = JsonSerializer.Serialize(command.DadosVenda);

        // Create attendance domain entity
        var atendimento = Atendimento.Criar(
            protocolo,
            command.Cpf,
            command.NumVendaFk,
            dadosVendaJson);

        // Persist the attendance
        await _atendimentoRepository.AddAsync(atendimento, cancellationToken);

        return protocolo;
    }
}

/// <summary>
/// Repository interface for attendance records.
/// </summary>
public interface IAtendimentoRepository
{
    Task AddAsync(Atendimento atendimento, CancellationToken cancellationToken);
}
