using System.Security.Cryptography;
using System.Text;

namespace KnowledgeVault.Infrastructure.Text;

public static class DocumentContentHash
{
    public static string Sha256Hex(string? content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content ?? string.Empty));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
