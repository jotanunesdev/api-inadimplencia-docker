using System.Text.RegularExpressions;

namespace ApiInadimplencia.Domain.Ocorrencias;

/// <summary>
/// Represents an occurrence registered for a delinquent sale.
/// </summary>
public class Ocorrencia
{
    /// <summary>
    /// Gets the unique identifier.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the sale number this occurrence relates to.
    /// </summary>
    public int NumVendaFk { get; private set; }

    /// <summary>
    /// Gets the username of the user who registered the occurrence.
    /// </summary>
    public string NomeUsuarioFk { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the description of the occurrence.
    /// </summary>
    public string Descricao { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the status of the occurrence.
    /// </summary>
    public string StatusOcorrencia { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the date of the occurrence.
    /// </summary>
    public DateTime DtOcorrencia { get; private set; }

    /// <summary>
    /// Gets the time of the occurrence.
    /// </summary>
    public string HoraOcorrencia { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the next action, when present.
    /// </summary>
    public string? ProximaAcao { get; private set; }

    /// <summary>
    /// Gets the protocol, when present.
    /// </summary>
    public string? Protocolo { get; private set; }

    /// <summary>
    /// Creates a new occurrence with validations.
    /// </summary>
    public static Ocorrencia Criar(
        int numVendaFk,
        string nomeUsuarioFk,
        string descricao,
        string statusOcorrencia,
        DateTime dtOcorrencia,
        string horaOcorrencia,
        string? proximaAcao = null,
        string? protocolo = null)
    {
        // Validations
        if (numVendaFk <= 0)
        {
            throw new ArgumentException("NUM_VENDA must be positive.", nameof(numVendaFk));
        }

        if (string.IsNullOrWhiteSpace(nomeUsuarioFk))
        {
            throw new ArgumentException("NomeUsuarioFk is required.", nameof(nomeUsuarioFk));
        }

        if (string.IsNullOrWhiteSpace(descricao))
        {
            throw new ArgumentException("Descricao is required.", nameof(descricao));
        }

        if (string.IsNullOrWhiteSpace(statusOcorrencia))
        {
            throw new ArgumentException("StatusOcorrencia is required.", nameof(statusOcorrencia));
        }

        if (dtOcorrencia == default)
        {
            throw new ArgumentException("DtOcorrencia is required.", nameof(dtOcorrencia));
        }

        if (string.IsNullOrWhiteSpace(horaOcorrencia))
        {
            throw new ArgumentException("HoraOcorrencia is required.", nameof(horaOcorrencia));
        }

        if (!Regex.IsMatch(horaOcorrencia, "^([01]?[0-9]|2[0-3]):[0-5][0-9]$"))
        {
            throw new ArgumentException("HoraOcorrencia must be in HH:MM format.", nameof(horaOcorrencia));
        }

        return new Ocorrencia
        {
            Id = Guid.NewGuid(),
            NumVendaFk = numVendaFk,
            NomeUsuarioFk = nomeUsuarioFk.Trim(),
            Descricao = descricao.Trim(),
            StatusOcorrencia = statusOcorrencia.Trim(),
            DtOcorrencia = dtOcorrencia,
            HoraOcorrencia = horaOcorrencia.Trim(),
            ProximaAcao = proximaAcao?.Trim(),
            Protocolo = protocolo?.Trim()
        };
    }

    /// <summary>
    /// Updates the occurrence.
    /// </summary>
    public void Atualizar(
        string? descricao = null,
        string? statusOcorrencia = null,
        DateTime? dtOcorrencia = null,
        string? horaOcorrencia = null,
        string? proximaAcao = null,
        string? protocolo = null)
    {
        if (descricao != null) Descricao = descricao;
        if (statusOcorrencia != null) StatusOcorrencia = statusOcorrencia;
        if (dtOcorrencia.HasValue) DtOcorrencia = dtOcorrencia.Value;
        if (horaOcorrencia != null) HoraOcorrencia = horaOcorrencia;
        if (proximaAcao != null) ProximaAcao = proximaAcao;
        if (protocolo != null) Protocolo = protocolo;
    }
}
