namespace ApiInadimplencia.Application.Abstractions.Integrations;

/// <summary>
/// Constraint type accepted by Fluig dataset-handle/search endpoint.
/// Mirrors the legacy Node.js mapping (1=MUST, 2=MUST_NOT, 3=SHOULD).
/// </summary>
public enum FluigConstraintType
{
    Must = 1,
    MustNot = 2,
    Should = 3,
}

/// <summary>
/// Constraint pair sent to Fluig dataset-handle/search.
/// Matches the legacy Node.js helper <c>buildConstraint</c>.
/// </summary>
/// <param name="Field">Constraint field name.</param>
/// <param name="InitialValue">Initial value (lower bound).</param>
/// <param name="FinalValue">Final value (upper bound) - usually equal to InitialValue.</param>
/// <param name="Type">Constraint operator (defaults to MUST).</param>
public sealed record FluigConstraint(
    string Field,
    string InitialValue,
    string? FinalValue = null,
    FluigConstraintType Type = FluigConstraintType.Must);

/// <summary>
/// Request payload for a Fluig dataset search. Mirrors the option bag accepted
/// by the legacy Node.js <c>fetchDataset(name, { fields, order, constraints })</c>.
/// </summary>
public sealed record FluigDatasetRequest(
    string DatasetName,
    IReadOnlyList<string>? Fields = null,
    IReadOnlyList<string>? OrderBy = null,
    IReadOnlyList<FluigConstraint>? Constraints = null);
