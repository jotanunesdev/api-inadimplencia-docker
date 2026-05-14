using ApiInadimplencia.Domain.SerasaPefin;

namespace ApiInadimplencia.Domain.SerasaPefin;

/// <summary>
/// Represents a Serasa PEFIN solicitation.
/// </summary>
public class SerasaPefinSolicitacao
{
    /// <summary>
    /// Gets the unique identifier.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the sale number.
    /// </summary>
    public int NumVendaFk { get; private set; }

    /// <summary>
    /// Gets the record type (Principal or Garantidor).
    /// </summary>
    public SerasaPefinRecordType TipoRegistro { get; private set; }

    /// <summary>
    /// Gets the Serasa transaction ID.
    /// </summary>
    public string? TransactionId { get; private set; }

    /// <summary>
    /// Gets the current status.
    /// </summary>
    public SerasaPefinStatus Status { get; private set; }

    /// <summary>
    /// Gets the request payload (JSON).
    /// </summary>
    public string PayloadJson { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the response from Serasa (JSON).
    /// </summary>
    public string? RespostaJson { get; private set; }

    /// <summary>
    /// Gets the error message, if any.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Gets the creation timestamp.
    /// </summary>
    public DateTime CriadoEm { get; private set; }

    /// <summary>
    /// Gets the timestamp when sent to Serasa.
    /// </summary>
    public DateTime? EnviadoEm { get; private set; }

    /// <summary>
    /// Gets the timestamp when completed.
    /// </summary>
    public DateTime? CompletadoEm { get; private set; }

    /// <summary>
    /// Creates a new Serasa PEFIN solicitation.
    /// </summary>
    public static SerasaPefinSolicitacao Criar(
        int numVendaFk,
        SerasaPefinRecordType tipoRegistro,
        string payloadJson)
    {
        return new SerasaPefinSolicitacao
        {
            Id = Guid.NewGuid(),
            NumVendaFk = numVendaFk,
            TipoRegistro = tipoRegistro,
            Status = SerasaPefinStatus.PendenteEnvio,
            PayloadJson = payloadJson,
            CriadoEm = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Marks as sent to Serasa.
    /// </summary>
    public void MarcarComoEnviado(string transactionId)
    {
        TransactionId = transactionId;
        Status = SerasaPefinStatus.EnviadoSerasa;
        EnviadoEm = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks as waiting for return.
    /// </summary>
    public void MarcarAguardandoRetorno()
    {
        Status = SerasaPefinStatus.AguardandoRetorno;
    }

    /// <summary>
    /// Marks as successfully completed.
    /// </summary>
    public void MarcarComoSucesso(string respostaJson)
    {
        Status = TipoRegistro == SerasaPefinRecordType.Principal
            ? SerasaPefinStatus.NegativadoSucesso
            : SerasaPefinStatus.BaixadoSucesso;
        RespostaJson = respostaJson;
        CompletadoEm = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks as failed.
    /// </summary>
    public void MarcarComoErro(string errorMessage)
    {
        Status = TipoRegistro == SerasaPefinRecordType.Principal
            ? SerasaPefinStatus.NegativadoErro
            : SerasaPefinStatus.BaixadoErro;
        ErrorMessage = errorMessage;
        CompletadoEm = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks as removal sent.
    /// </summary>
    public void MarcarBaixaEnviada()
    {
        Status = SerasaPefinStatus.BaixaEnviada;
        EnviadoEm = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks as removal waiting for return.
    /// </summary>
    public void MarcarBaixaAguardandoRetorno()
    {
        Status = SerasaPefinStatus.BaixaAguardandoRetorno;
    }
}
