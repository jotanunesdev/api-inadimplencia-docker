namespace ApiInadimplencia.Application.Features.Notifications.Dtos;

/// <summary>
/// DTO for notification responses.
/// </summary>
public record NotificationDto
{
    public Guid Id { get; init; }
    public string Tipo { get; init; } = string.Empty;
    public string Usuario { get; init; } = string.Empty;
    public int NumVenda { get; init; }
    public DateOnly? ProximaAcaoDia { get; init; }
    public string Mensagem { get; init; } = string.Empty;
    public bool Lida { get; init; }
    public DateTime CriadaEm { get; init; }
}
