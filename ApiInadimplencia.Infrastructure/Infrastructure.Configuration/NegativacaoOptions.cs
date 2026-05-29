using System.ComponentModel.DataAnnotations;

namespace ApiInadimplencia.Infrastructure.Configuration;

/// <summary>
/// Configuration options for the negativacao (Serasa PEFIN) workflow.
/// </summary>
public sealed class NegativacaoOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Negativacao";

    /// <summary>
    /// List of usernames authorized to approve negativacao requests.
    /// </summary>
    public string[] UsuariosAprovadores { get; set; } = [];

    /// <summary>
    /// Number of approvers required for approval (quorum).
    /// </summary>
    [Range(1, 10)]
    public int QuorumAprovacao { get; set; } = 1;

    /// <summary>
    /// Minimum days overdue for a debt to be eligible for negativacao.
    /// </summary>
    [Range(1, 365)]
    public int DiasAtrasoMinimo { get; set; } = 60;

    /// <summary>
    /// Maximum failed password attempts before lockout.
    /// </summary>
    [Range(1, 10)]
    public int MaxTentativasSenha { get; set; } = 3;

    /// <summary>
    /// Lockout duration in minutes after max failed attempts.
    /// </summary>
    [Range(1, 1440)]
    public int LockoutMinutos { get; set; } = 15;

    /// <summary>
    /// Time window in minutes for counting failed attempts.
    /// </summary>
    [Range(1, 60)]
    public int JanelaTentativasMinutos { get; set; } = 5;
}
