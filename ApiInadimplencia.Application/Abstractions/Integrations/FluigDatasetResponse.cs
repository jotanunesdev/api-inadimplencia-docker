namespace ApiInadimplencia.Application.Abstractions.Integrations;

/// <summary>
/// Response from a Fluig dataset search. Carries the values rows returned by
/// the dataset (each row is a dictionary of column → value as raw string).
/// </summary>
/// <param name="Values">Rows returned by the dataset, in order.</param>
public sealed record FluigDatasetResponse(IReadOnlyList<IReadOnlyDictionary<string, string?>> Values)
{
    /// <summary>Empty response (no values).</summary>
    public static readonly FluigDatasetResponse Empty = new(Array.Empty<IReadOnlyDictionary<string, string?>>());
}
