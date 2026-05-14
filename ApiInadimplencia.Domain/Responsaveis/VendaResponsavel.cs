using ApiInadimplencia.Domain.Common;
using ApiInadimplencia.Domain.Events;

namespace ApiInadimplencia.Domain.Responsaveis;

/// <summary>
/// Represents a responsible user assignment for a sale.
/// </summary>
public class VendaResponsavel
{
    private readonly List<DomainEvent> _domainEvents = [];

    /// <summary>
    /// Gets the sale number.
    /// </summary>
    public int NumVendaFk { get; private set; }

    /// <summary>
    /// Gets the username of the responsible user.
    /// </summary>
    public string Username { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the timestamp when the assignment was created.
    /// </summary>
    public DateTime AtribuidoEm { get; private set; }

    /// <summary>
    /// Gets the username of the admin who performed the assignment.
    /// </summary>
    public string AtribuidoPor { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the domain events raised by this entity.
    /// </summary>
    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Creates a new sale responsibility assignment.
    /// </summary>
    public static VendaResponsavel Criar(
        int numVendaFk,
        string username,
        string adminUserCode)
    {
        var responsavel = new VendaResponsavel
        {
            NumVendaFk = numVendaFk,
            Username = username,
            AtribuidoEm = DateTime.UtcNow,
            AtribuidoPor = adminUserCode
        };

        responsavel._domainEvents.Add(new ResponsavelAtribuidoEvent(
            numVendaFk,
            null,
            username,
            adminUserCode,
            DateTimeOffset.UtcNow));

        return responsavel;
    }

    /// <summary>
    /// Updates the responsible user.
    /// </summary>
    public void AtualizarResponsavel(string novoUsername, string adminUserCode)
    {
        var anteriorUsername = Username;
        Username = novoUsername;
        AtribuidoEm = DateTime.UtcNow;
        AtribuidoPor = adminUserCode;

        _domainEvents.Add(new ResponsavelAtribuidoEvent(
            NumVendaFk,
            anteriorUsername,
            novoUsername,
            adminUserCode,
            DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Clears the domain events.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
