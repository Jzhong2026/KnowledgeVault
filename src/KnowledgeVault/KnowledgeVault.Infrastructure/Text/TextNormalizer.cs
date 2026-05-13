namespace KnowledgeVault.Infrastructure.Text;

public static class TextNormalizer
{
    public static string NormalizeName(string value)
    {
        return value.Trim().ToUpperInvariant();
    }
}
