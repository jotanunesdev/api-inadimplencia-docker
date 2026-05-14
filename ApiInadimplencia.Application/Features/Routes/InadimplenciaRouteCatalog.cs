namespace ApiInadimplencia.Application.Features.Routes;

/// <summary>
/// Describes one migrated or planned HTTP endpoint.
/// </summary>
/// <param name="Method">HTTP method.</param>
/// <param name="Path">Endpoint path.</param>
/// <param name="Feature">Feature name.</param>
/// <param name="Status">Migration status.</param>
/// <param name="Notes">Short implementation notes.</param>
public sealed record EndpointDescriptor(
    string Method,
    string Path,
    string Feature,
    string Status,
    string Notes);

/// <summary>
/// Catalog of the Node inadimplencia contract tracked by the .NET migration.
/// </summary>
public static class InadimplenciaRouteCatalog
{
    /// <summary>
    /// Gets the contract endpoints known from the source module.
    /// </summary>
    public static IReadOnlyList<EndpointDescriptor> Endpoints { get; } =
    [
        new("GET", "/health", "Health", "migrated", "Global API health."),
        new("GET", "/inadimplencia/health", "Health", "migrated", "Module health."),
        new("GET", "/inadimplencia/contracts", "Documentation", "migrated", "Returns this route catalog."),
        new("GET", "/inadimplencia", "Carteira", "migrated", "Typed query handler for DW.fat_analise_inadimplencia_v4."),
        new("GET", "/inadimplencia/cpf/{cpf}", "Carteira", "migrated", "Typed query handler with CPF normalization."),
        new("GET", "/inadimplencia/num-venda/{numVenda}", "Carteira", "migrated", "Typed query handler for sale lookup."),
        new("GET", "/inadimplencia/responsavel/{nome}", "Carteira", "migrated", "Typed query handler for responsible user lookup."),
        new("GET", "/inadimplencia/cliente/{nomeCliente}", "Carteira", "migrated", "Typed query handler for client name search."),
        new("GET", "/proximas-acoes", "Proximas Acoes", "migrated", "Typed query handler (read-only)."),
        new("GET", "/proximas-acoes/{numVenda}", "Proximas Acoes", "migrated", "Typed query handler (read-only)."),
        new("POST", "/proximas-acoes", "Proximas Acoes", "blocked", "Mutations must be done via ocorrencias."),
        new("PUT", "/proximas-acoes/{numVenda}", "Proximas Acoes", "blocked", "Mutations must be done via ocorrencias."),
        new("DELETE", "/proximas-acoes/{numVenda}", "Proximas Acoes", "blocked", "Mutations must be done via ocorrencias."),
        new("GET", "/ocorrencias", "Ocorrencias", "migrated", "Typed query handler for list."),
        new("POST", "/ocorrencias", "Ocorrencias", "migrated", "Typed command handler with FK guard."),
        new("GET", "/ocorrencias/{id}", "Ocorrencias", "migrated", "Typed query handler by ID."),
        new("GET", "/ocorrencias/num-venda/{numVenda}", "Ocorrencias", "migrated", "Typed query handler by sale number."),
        new("GET", "/ocorrencias/protocolo/{protocolo}", "Ocorrencias", "migrated", "Typed query handler by protocol."),
        new("PUT", "/ocorrencias/{id}", "Ocorrencias", "migrated", "Typed command handler for update."),
        new("DELETE", "/ocorrencias/{id}", "Ocorrencias", "migrated", "Typed command handler for delete."),
        new("POST", "/atendimentos", "Atendimentos", "migrated", "Typed command handler with transactional protocol generation."),
        new("GET", "/atendimentos/cpf/{cpf}", "Atendimentos", "migrated", "Typed query handler by CPF with normalization."),
        new("GET", "/atendimentos/num-venda/{numVenda}", "Atendimentos", "migrated", "Typed query handler by sale number."),
        new("GET", "/atendimentos/protocolo/{protocolo}", "Atendimentos", "migrated", "Typed query handler by protocol."),
        new("GET", "/atendimentos/cliente/{nomeCliente}", "Atendimentos", "migrated", "Typed query handler by customer name."),
        new("GET", "/usuarios", "Usuarios", "migrated", "Typed query handler."),
        new("POST", "/usuarios", "Usuarios", "migrated", "Typed command handler for idempotent upsert."),
        new("GET", "/usuarios/{nome}", "Usuarios", "migrated", "Typed query handler."),
        new("PUT", "/usuarios/{nome}", "Usuarios", "migrated", "Typed command handler for update."),
        new("DELETE", "/usuarios/{nome}", "Usuarios", "migrated", "Typed command handler for delete."),
        new("GET", "/responsaveis", "Responsaveis", "migrated", "Typed query handler."),
        new("POST", "/responsaveis", "Responsaveis", "migrated", "Typed command handler with admin validation."),
        new("GET", "/responsaveis/{numVenda}", "Responsaveis", "migrated", "Typed query handler."),
        new("PUT", "/responsaveis/{numVenda}", "Responsaveis", "migrated", "Typed command handler for update."),
        new("DELETE", "/responsaveis/{numVenda}", "Responsaveis", "migrated", "Typed command handler for delete."),
        new("GET", "/kanban-status", "Kanban", "migrated", "Typed query handler."),
        new("POST", "/kanban-status", "Kanban", "migrated", "Typed command handler with status normalization."),
        new("GET", "/dashboard/kpis", "Dashboard", "migrated", "Typed query handler for KPIs."),
        new("GET", "/dashboard/{metric}", "Dashboard", "migrated", "Typed query handler for metrics with filters."),
        new("GET", "/fiadores/num-venda/{numVenda}", "Fiadores", "migrated", "Typed query handler for DW.vw_fiadores_por_venda."),
        new("GET", "/fiadores/cpf/{cpf}", "Fiadores", "migrated", "Typed query handler with CPF normalization."),
        new("GET", "/notifications", "Notifications", "migrated", "Typed query handler for paginated snapshot."),
        new("GET", "/notifications/stream", "Notifications", "migrated", "SSE hub with snapshot and heartbeat."),
        new("PUT", "/notifications/{id}/read", "Notifications", "migrated", "Typed command handler."),
        new("PUT", "/notifications/read-all", "Notifications", "migrated", "Typed command handler."),
        new("DELETE", "/notifications/{id}", "Notifications", "migrated", "Typed command handler with read validation."),
        new("GET", "/relatorios/ficha-financeira", "Relatorios RM", "migrated", "Typed command handler via Fluig/RM gateway."),
        new("GET", "/serasa-pefin/vendas/{numVenda}/preview", "Serasa PEFIN", "migrated", "Typed query handler with domain validation."),
        new("POST", "/serasa-pefin/vendas/{numVenda}/negativacoes", "Serasa PEFIN", "migrated", "Typed command handler with outbox pattern."),
        new("GET", "/serasa-pefin/vendas/{numVenda}/negativacoes", "Serasa PEFIN", "migrated", "Typed query handler for history."),
        new("GET", "/serasa-pefin/acompanhamento/{transactionId}", "Serasa PEFIN", "migrated", "Typed query handler for tracking."),
        new("GET", "/serasa-pefin/negativacoes/{id}", "Serasa PEFIN", "migrated", "Typed query handler for details."),
        new("POST", "/serasa-pefin/webhooks/{tipo}/{resultado}", "Serasa PEFIN", "partial", "Webhook idempotency pending implementation."),
    ];
}

