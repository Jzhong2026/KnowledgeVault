using KnowledgeVault.Infrastructure.Exceptions;

namespace KnowledgeVault.Infrastructure.Text;

/// <summary>
/// Single source of truth for trimming/length validation of request text fields.
/// Replaces the per-provider RequireText/CleanOptional copies.
/// </summary>
public static class RequestText
{
    /// <summary>Requires a non-empty value, trims it, and enforces a max length.</summary>
    public static string Require(string? value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException($"{fieldName} is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ValidationException($"{fieldName} must be {maxLength} characters or fewer.");
        }

        return trimmed;
    }

    /// <summary>Returns null for blank input; otherwise trims and enforces a max length.</summary>
    public static string? Optional(string? value, int maxLength)
    {
        return Optional(value, "Value", maxLength);
    }

    /// <summary>Returns null for blank input; otherwise trims and enforces a max length.</summary>
    public static string? Optional(string? value, string fieldName, int maxLength)
    {
        return string.IsNullOrWhiteSpace(value) ? null : Require(value, fieldName, maxLength);
    }
}
