using ApiInadimplencia.Domain.Atendimentos;
using ApiInadimplencia.Domain.Common;
using ApiInadimplencia.Domain.Kanban;
using ApiInadimplencia.Domain.Notifications;
using ApiInadimplencia.Domain.Ocorrencias;
using ApiInadimplencia.Domain.Responsaveis;
using ApiInadimplencia.Domain.SerasaPefin;
using ApiInadimplencia.Domain.Usuarios;
using ApiInadimplencia.Infrastructure.Configuration;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Configuration;

namespace ApiInadimplencia.Infrastructure.Persistence.SqlServer;

/// <summary>
/// Entity Framework Core DbContext for the inadimplencia module.
/// </summary>
public class InadimplenciaDbContext : DbContext
{
    private readonly SqlServerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="InadimplenciaDbContext"/> class.
    /// </summary>
    /// <param name="options">DbContext options.</param>
    /// <param name="configuration">Application configuration.</param>
    public InadimplenciaDbContext(
        DbContextOptions<InadimplenciaDbContext> options,
        IConfiguration configuration)
        : base(options)
    {
        _options = configuration
            .GetSection(SqlServerOptions.SectionName)
            .Get<SqlServerOptions>()
            ?? new SqlServerOptions();
    }

    /// <summary>
    /// Gets or sets the occurrences DbSet.
    /// </summary>
    public DbSet<Ocorrencia> Ocorrencias => Set<Ocorrencia>();

    /// <summary>
    /// Gets or sets the attendances DbSet.
    /// </summary>
    public DbSet<Atendimento> Atendimentos => Set<Atendimento>();

    /// <summary>
    /// Gets or sets the users DbSet.
    /// </summary>
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    /// <summary>
    /// Gets or sets the sale responsibilities DbSet.
    /// </summary>
    public DbSet<VendaResponsavel> VendaResponsaveis => Set<VendaResponsavel>();

    /// <summary>
    /// Gets or sets the kanban statuses DbSet.
    /// </summary>
    public DbSet<KanbanStatusEntity> KanbanStatuses => Set<KanbanStatusEntity>();

    /// <summary>
    /// Gets or sets the notifications DbSet.
    /// </summary>
    public DbSet<InadNotificacao> InadNotificacoes => Set<InadNotificacao>();

    /// <summary>
    /// Gets or sets the Serasa PEFIN solicitations DbSet.
    /// </summary>
    public DbSet<SerasaPefinSolicitacao> SerasaPefinSolicitacoes => Set<SerasaPefinSolicitacao>();

    /// <summary>
    /// Gets or sets the Serasa PEFIN webhooks DbSet.
    /// </summary>
    public DbSet<SerasaPefinWebhook> SerasaPefinWebhooks => Set<SerasaPefinWebhook>();

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer(_options.ConnectionString, options =>
            {
                options.CommandTimeout(_options.CommandTimeoutSeconds);
                options.EnableRetryOnFailure(maxRetryCount: 3);
            });
        }
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // MassTransit EF Core outbox/inbox entities
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        // Ocorrencia configuration
        modelBuilder.Entity<Ocorrencia>(entity =>
        {
            entity.ToTable("OCORRENCIAS", "dbo");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("NEWID()");
            entity.Property(e => e.NumVendaFk).IsRequired();
            entity.Property(e => e.NomeUsuarioFk).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Descricao).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.StatusOcorrencia).IsRequired().HasMaxLength(50);
            entity.Property(e => e.DtOcorrencia).IsRequired();
            entity.Property(e => e.HoraOcorrencia).IsRequired().HasMaxLength(10);
            entity.Property(e => e.ProximaAcao).HasMaxLength(500);
            entity.Property(e => e.Protocolo).HasMaxLength(50);
        });

        // Atendimento configuration
        modelBuilder.Entity<Atendimento>(entity =>
        {
            entity.ToTable("ATENDIMENTOS", "dbo");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("NEWID()");
            entity.Property(e => e.Protocolo).IsRequired().HasMaxLength(13);
            entity.Property(e => e.Cpf).IsRequired().HasMaxLength(14);
            entity.Property(e => e.NumVendaFk).IsRequired();
            entity.Property(e => e.DadosVendaJson).IsRequired();
            entity.Property(e => e.CriadoEm).IsRequired();
        });

        // Usuario configuration
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.ToTable("USUARIO", "dbo");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserCode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Nome).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Perfil).IsRequired();
            entity.Property(e => e.CorHex)
                .IsRequired()
                .HasMaxLength(7)
                .HasConversion(new ValueConverter<HexColor, string>(
                    v => v.Value,
                    v => HexColor.Create(v)));
            entity.Property(e => e.CriadoEm).IsRequired();
            entity.HasIndex(e => e.UserCode).IsUnique();
            entity.HasIndex(e => e.Nome);
        });

        // VendaResponsavel configuration
        modelBuilder.Entity<VendaResponsavel>(entity =>
        {
            entity.ToTable("VENDA_RESPONSAVEL", "dbo");
            entity.HasKey(e => e.NumVendaFk);
            entity.Property(e => e.NumVendaFk).IsRequired();
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.Property(e => e.AtribuidoEm).IsRequired();
            entity.Property(e => e.AtribuidoPor).IsRequired().HasMaxLength(100);
            entity.Ignore(e => e.DomainEvents);
        });

        // KanbanStatusEntity configuration
        modelBuilder.Entity<KanbanStatusEntity>(entity =>
        {
            entity.ToTable("KANBAN_STATUS", "dbo");
            entity.HasKey(e => new { e.NumVendaFk, e.ProximaAcao });
            entity.Property(e => e.NumVendaFk).IsRequired();
            entity.Property(e => e.ProximaAcao).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.StatusData).IsRequired();
        });

        // InadNotificacao configuration.
        // The physical table dbo.INAD_NOTIFICACOES uses LEGACY uppercase-snake column
        // names (USUARIO_DESTINATARIO, NUM_VENDA, PAYLOAD, DT_CRIACAO, DT_EXCLUSAO,
        // TIPO as varchar). Map each property explicitly so EF Core writes/reads the
        // existing schema instead of failing with "Invalid column name".
        modelBuilder.Entity<InadNotificacao>(entity =>
        {
            entity.ToTable("INAD_NOTIFICACOES", "dbo");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("NEWID()");

            // TIPO is stored as varchar(32) using UPPER_SNAKE_CASE labels
            // (e.g. SOLICITACAO_NEGATIVACAO) for compatibility with legacy SQL
            // queries and the frontend.
            entity.Property(e => e.Tipo)
                .HasColumnName("TIPO")
                .HasMaxLength(32)
                .IsRequired()
                .HasConversion(
                    v => NotificationTypeToLegacyString(v),
                    v => LegacyStringToNotificationType(v));

            entity.Property(e => e.Usuario)
                .HasColumnName("USUARIO_DESTINATARIO")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.NumVenda)
                .HasColumnName("NUM_VENDA")
                .IsRequired();

            // PROXIMA_ACAO_DIA is a COMPUTED column in the legacy schema (derived from
            // the datetime PROXIMA_ACAO). EF must never try to INSERT/UPDATE it; the
            // value is read back from the database after save.
            var proximaAcaoDiaProp = entity.Property(e => e.ProximaAcaoDia)
                .HasColumnName("PROXIMA_ACAO_DIA")
                .ValueGeneratedOnAddOrUpdate();
            proximaAcaoDiaProp.Metadata.SetBeforeSaveBehavior(Microsoft.EntityFrameworkCore.Metadata.PropertySaveBehavior.Ignore);
            proximaAcaoDiaProp.Metadata.SetAfterSaveBehavior(Microsoft.EntityFrameworkCore.Metadata.PropertySaveBehavior.Ignore);

            // Plain message persisted in the PAYLOAD column. Legacy callers store JSON
            // there; the response mapper tolerates non-JSON values (returns empty dict).
            entity.Property(e => e.Mensagem)
                .HasColumnName("PAYLOAD")
                .IsRequired();

            entity.Property(e => e.DedupeKey)
                .HasColumnName("DEDUPE_KEY")
                .HasMaxLength(100);

            entity.Property(e => e.Lida)
                .HasColumnName("LIDA")
                .IsRequired()
                .HasDefaultValue(false);

            entity.Property(e => e.CriadaEm)
                .HasColumnName("DT_CRIACAO")
                .IsRequired();

            entity.Property(e => e.ExcluidaEm)
                .HasColumnName("DT_EXCLUSAO");

            entity.HasIndex(e => new { e.Tipo, e.Usuario, e.NumVenda, e.ProximaAcaoDia }).IsUnique();
            entity.HasIndex(e => e.DedupeKey);
        });

        // SerasaPefinSolicitacao configuration
        modelBuilder.Entity<SerasaPefinSolicitacao>(entity =>
        {
            entity.ToTable("SERASA_PEFIN_SOLICITACOES", "dbo");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("NEWID()");
            entity.Property(e => e.NumVendaFk).IsRequired();
            entity.Property(e => e.TipoRegistro).IsRequired();
            entity.Property(e => e.TransactionId).HasMaxLength(100);
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.PayloadJson).IsRequired();
            entity.Property(e => e.RespostaJson);
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
            entity.Property(e => e.CriadoEm).IsRequired();
            entity.Property(e => e.EnviadoEm);
            entity.Property(e => e.CompletadoEm);
            entity.HasIndex(e => e.TransactionId);
        });

        // SerasaPefinWebhook configuration
        modelBuilder.Entity<SerasaPefinWebhook>(entity =>
        {
            entity.ToTable("SERASA_PEFIN_WEBHOOKS", "dbo");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("NEWID()");
            entity.Property(e => e.Uuid).IsRequired().HasMaxLength(100);
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Resultado).IsRequired().HasMaxLength(50);
            entity.Property(e => e.TransactionId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PayloadJson).IsRequired();
            entity.Property(e => e.RecebidoEm).IsRequired();
            entity.HasIndex(e => e.Uuid).IsUnique();
            entity.HasIndex(e => e.TransactionId);
        });
    }

    /// <summary>
    /// Maps <see cref="NotificationType"/> values to the UPPER_SNAKE_CASE strings
    /// stored in the legacy <c>TIPO</c> column of <c>dbo.INAD_NOTIFICACOES</c>.
    /// </summary>
    private static string NotificationTypeToLegacyString(NotificationType type) => type switch
    {
        NotificationType.VendaAtribuida => "VENDA_ATRIBUIDA",
        NotificationType.VendaAtrasada => "VENDA_ATRASADA",
        NotificationType.SolicitacaoNegativacao => "SOLICITACAO_NEGATIVACAO",
        NotificationType.AprovacaoNegativacao => "APROVACAO_NEGATIVACAO",
        NotificationType.RejeicaoNegativacao => "REJEICAO_NEGATIVACAO",
        NotificationType.RetornoSerasaSucesso => "RETORNO_SERASA_SUCESSO",
        NotificationType.RetornoSerasaErro => "RETORNO_SERASA_ERRO",
        _ => type.ToString().ToUpperInvariant(),
    };

    /// <summary>
    /// Reverse of <see cref="NotificationTypeToLegacyString"/>; tolerates legacy values
    /// or PascalCase enum names. Returns <see cref="NotificationType.VendaAtrasada"/>
    /// as a safe fallback for unknown values to keep reads non-fatal.
    /// </summary>
    private static NotificationType LegacyStringToNotificationType(string value) => (value ?? string.Empty).Trim().ToUpperInvariant() switch
    {
        "VENDA_ATRIBUIDA" => NotificationType.VendaAtribuida,
        "VENDA_ATRASADA" => NotificationType.VendaAtrasada,
        "SOLICITACAO_NEGATIVACAO" => NotificationType.SolicitacaoNegativacao,
        "APROVACAO_NEGATIVACAO" => NotificationType.AprovacaoNegativacao,
        "REJEICAO_NEGATIVACAO" => NotificationType.RejeicaoNegativacao,
        "RETORNO_SERASA_SUCESSO" => NotificationType.RetornoSerasaSucesso,
        "RETORNO_SERASA_ERRO" => NotificationType.RetornoSerasaErro,
        _ when Enum.TryParse<NotificationType>(value, ignoreCase: true, out var parsed) => parsed,
        _ => NotificationType.VendaAtrasada,
    };
}
