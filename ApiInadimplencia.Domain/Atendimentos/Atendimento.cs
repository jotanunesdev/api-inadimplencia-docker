using System.Text.RegularExpressions;

namespace ApiInadimplencia.Domain.Atendimentos;

/// <summary>
/// Represents an attendance record with a generated protocol.
/// </summary>
public class Atendimento
{
    /// <summary>
    /// Gets the unique identifier.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the protocol number in AAAAMMDD##### format.
    /// </summary>
    public string Protocolo { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the CPF of the customer.
    /// </summary>
    public string Cpf { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the sale number.
    /// </summary>
    public int NumVendaFk { get; private set; }

    /// <summary>
    /// Gets the JSON snapshot of the sale data.
    /// </summary>
    public string DadosVendaJson { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the creation timestamp.
    /// </summary>
    public DateTime CriadoEm { get; private set; }

    /// <summary>
    /// Creates a new attendance record with validations.
    /// </summary>
    public static Atendimento Criar(
        string protocolo,
        string cpf,
        int numVendaFk,
        string dadosVendaJson)
    {
        // Validations
        if (string.IsNullOrWhiteSpace(protocolo))
        {
            throw new ArgumentException("Protocolo is required.", nameof(protocolo));
        }

        if (!Regex.IsMatch(protocolo, @"^\d{13}$"))
        {
            throw new ArgumentException("Protocolo must be in AAAAMMDD##### format (13 digits).", nameof(protocolo));
        }

        if (string.IsNullOrWhiteSpace(cpf))
        {
            throw new ArgumentException("Cpf is required.", nameof(cpf));
        }

        if (numVendaFk <= 0)
        {
            throw new ArgumentException("NUM_VENDA must be positive.", nameof(numVendaFk));
        }

        if (string.IsNullOrWhiteSpace(dadosVendaJson))
        {
            throw new ArgumentException("DadosVendaJson is required.", nameof(dadosVendaJson));
        }

        return new Atendimento
        {
            Id = Guid.NewGuid(),
            Protocolo = protocolo.Trim(),
            Cpf = cpf.Trim(),
            NumVendaFk = numVendaFk,
            DadosVendaJson = dadosVendaJson.Trim(),
            CriadoEm = DateTime.UtcNow
        };
    }
}
