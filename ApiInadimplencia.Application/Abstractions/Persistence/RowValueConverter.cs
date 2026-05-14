using System.Globalization;

namespace ApiInadimplencia.Application.Abstractions.Persistence;

/// <summary>
/// Converte valores retornados por <see cref="ILegacySqlExecutor"/> para os tipos
/// esperados pelos DTOs de leitura. Centraliza o tratamento de coerções entre
/// tipos SQL (Int32, Int64, Decimal, Double, etc.) e tipos do domínio (string,
/// int, decimal, DateTime e seus nullable equivalentes), evitando
/// <see cref="InvalidCastException"/> quando o tipo declarado pela view/tabela
/// não coincide exatamente com o tipo do DTO.
/// </summary>
public static class RowValueConverter
{
    /// <summary>
    /// Obtém o valor da chave informada convertido para <typeparamref name="T"/>.
    /// Retorna <c>default(T)</c> quando o valor é nulo ou ausente.
    /// </summary>
    public static T GetValue<T>(IReadOnlyDictionary<string, object?> row, string key)
    {
        if (row is null || !row.TryGetValue(key, out var value) || value is null || value is DBNull)
        {
            return default!;
        }

        if (value is T typed)
        {
            return typed;
        }

        var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

        try
        {
            if (targetType == typeof(string))
            {
                return (T)(object)Convert.ToString(value, CultureInfo.InvariantCulture)!;
            }

            if (targetType.IsEnum)
            {
                return (T)Enum.Parse(targetType, value.ToString()!, ignoreCase: true);
            }

            if (targetType == typeof(Guid))
            {
                return (T)(object)Guid.Parse(value.ToString()!);
            }

            if (targetType == typeof(TimeSpan) && value is string ts)
            {
                return (T)(object)TimeSpan.Parse(ts, CultureInfo.InvariantCulture);
            }

            // Coerções numéricas/datas via IConvertible (cobre int<->long, decimal<->double, etc.).
            if (value is IConvertible)
            {
                return (T)Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            }

            return (T)value;
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            throw new InvalidCastException(
                $"Falha ao converter coluna '{key}' (valor='{value}', tipo origem='{value.GetType().FullName}') para '{typeof(T).FullName}'.",
                ex);
        }
    }
}
