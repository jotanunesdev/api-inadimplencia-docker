using System.ComponentModel.DataAnnotations;

namespace ApiInadimplencia.Infrastructure.Configuration;

/// <summary>
/// RabbitMQ configuration options.
/// </summary>
public class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";

    /// <summary>
    /// Gets or sets the RabbitMQ host.
    /// </summary>
    [Required]
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the RabbitMQ port.
    /// </summary>
    [Required]
    public int Port { get; set; } = 5672;

    /// <summary>
    /// Gets or sets the RabbitMQ username.
    /// </summary>
    [Required]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the RabbitMQ password.
    /// </summary>
    [Required]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the RabbitMQ virtual host.
    /// </summary>
    public string VirtualHost { get; set; } = "/";
}
