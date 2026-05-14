using ApiInadimplencia.Domain.Notifications;

namespace ApiInadimplencia.Domain.Notifications;

/// <summary>
/// Represents a notification persisted in the database.
/// </summary>
public class InadNotificacao
{
    /// <summary>
    /// Gets the unique identifier.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the notification type.
    /// </summary>
    public NotificationType Tipo { get; private set; }

    /// <summary>
    /// Gets the username this notification is for.
    /// </summary>
    public string Usuario { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the sale number.
    /// </summary>
    public int NumVenda { get; private set; }

    /// <summary>
    /// Gets the next action day.
    /// </summary>
    public DateOnly? ProximaAcaoDia { get; private set; }

    /// <summary>
    /// Gets the notification message.
    /// </summary>
    public string Mensagem { get; private set; } = string.Empty;

    /// <summary>
    /// Gets whether the notification has been read.
    /// </summary>
    public bool Lida { get; private set; }

    /// <summary>
    /// Gets the creation timestamp.
    /// </summary>
    public DateTime CriadaEm { get; private set; }

    /// <summary>
    /// Gets the soft delete timestamp.
    /// </summary>
    public DateTime? ExcluidaEm { get; private set; }

    /// <summary>
    /// Creates a new notification.
    /// </summary>
    public static InadNotificacao Criar(
        NotificationType tipo,
        string usuario,
        int numVenda,
        DateOnly? proximaAcaoDia,
        string mensagem)
    {
        return new InadNotificacao
        {
            Id = Guid.NewGuid(),
            Tipo = tipo,
            Usuario = usuario.ToLowerInvariant(),
            NumVenda = numVenda,
            ProximaAcaoDia = proximaAcaoDia,
            Mensagem = mensagem,
            Lida = false,
            CriadaEm = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Marks the notification as read.
    /// </summary>
    public void MarcarComoLida()
    {
        Lida = true;
    }

    /// <summary>
    /// Soft deletes the notification.
    /// </summary>
    public void Excluir()
    {
        if (!Lida)
        {
            throw new InvalidOperationException("Cannot delete unread notification.");
        }
        ExcluidaEm = DateTime.UtcNow;
    }
}
