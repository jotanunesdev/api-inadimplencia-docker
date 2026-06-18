using ApiInadimplencia.Application.Features.LoadTesting;

namespace ApiInadimplencia.Infrastructure.Monitoring;

internal sealed record LoadTestProfileDefinition(
    string Key,
    string Name,
    string Description,
    string ScriptName,
    int ExpectedDurationSeconds,
    int MaxVirtualUsers)
{
    public LoadTestProfileDto ToDto()
        => new(Key, Name, Description, ScriptName, ExpectedDurationSeconds, MaxVirtualUsers);
}

internal static class LoadTestProfiles
{
    public static readonly IReadOnlyList<LoadTestProfileDefinition> All =
    [
        new(
            "baseline",
            "Nivel 1 · Baseline",
            "Valida estabilidade inicial e latencia sob carga controlada.",
            "managed.js",
            90,
            8),
        new(
            "intenso",
            "Nivel 2 · Carga Intensa",
            "Simula carga sustentada de uso forte com crescimento progressivo.",
            "managed.js",
            300,
            32),
        new(
            "estresse-maximo",
            "Nivel 3 · Estresse Maximo",
            "Empurra a API ao limite com pico agressivo de concorrencia.",
            "managed.js",
            240,
            180),
    ];

    public static LoadTestProfileDefinition Get(string key)
        => All.FirstOrDefault(profile => string.Equals(profile.Key, key, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Unknown load test profile '{key}'.");
}
