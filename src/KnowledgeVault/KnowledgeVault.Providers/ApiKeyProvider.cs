using System.Security.Cryptography;
using KnowledgeVault.Contracts.ApiKeys;
using KnowledgeVault.Contracts.Providers;
using KnowledgeVault.Contracts.Security;
using KnowledgeVault.DataAccess;
using KnowledgeVault.Domain.Entities;
using KnowledgeVault.Infrastructure.Exceptions;
using KnowledgeVault.Infrastructure.Time;
using KnowledgeVault.Providers.Mapping;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeVault.Providers;

public static class ApiKeyScopes
{
    public const string DocumentsRead = "documents:read";
    public const string DocumentsWrite = "documents:write";
    public const string CommentsRead = "comments:read";
    public const string CommentsWrite = "comments:write";
    public const string ProjectsRead = "projects:read";

    public static IReadOnlyList<string> All { get; } =
    [
        DocumentsRead,
        DocumentsWrite,
        CommentsRead,
        CommentsWrite,
        ProjectsRead
    ];
}

public sealed class ApiKeyProvider(
    KnowledgeVaultDbContext dbContext,
    ICurrentUserContext currentUserContext,
    IDateTimeProvider dateTimeProvider) : IApiKeyProvider
{
    private const int DefaultExpiryDays = 365;
    private const int MinExpiryDays = 1;
    private const int MaxExpiryDays = 365;

    public async Task<ApiKeyCreatedDto> CreateAsync(CreateApiKeyRequest request, CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        var name = RequireText(request.Name, "Name", 64);

        var normalizedScopes = (request.Scopes ?? [])
            .Select(x => x.Trim().ToLowerInvariant())
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct()
            .ToArray();

        var invalid = normalizedScopes.Except(ApiKeyScopes.All, StringComparer.OrdinalIgnoreCase).ToArray();
        if (invalid.Length > 0)
        {
            throw new ValidationException($"Invalid scope(s): {string.Join(", ", invalid)}.");
        }

        var expiryDays = request.ExpiresInDays ?? DefaultExpiryDays;
        if (expiryDays < MinExpiryDays || expiryDays > MaxExpiryDays)
        {
            throw new ValidationException($"Expiry must be between {MinExpiryDays} and {MaxExpiryDays} days.");
        }

        var prefix = GenerateToken(6);
        var secret = GenerateToken(32);
        var fullKey = $"kv_{prefix}_{secret}";
        var secretHash = ComputeHash(secret);
        var now = dateTimeProvider.UtcNow;
        var expiresAt = now.AddDays(expiryDays);

        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Prefix = prefix,
            SecretHash = secretHash,
            Scopes = string.Join(",", normalizedScopes),
            ExpiresAt = expiresAt,
            CreatedAt = now
        };

        dbContext.ApiKeys.Add(apiKey);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ApiKeyCreatedDto(apiKey.Id, apiKey.Name, fullKey, apiKey.Prefix, expiresAt);
    }

    public async Task<IReadOnlyList<ApiKeyDto>> ListAsync(CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        var keys = await dbContext.ApiKeys
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return keys.Select(x => x.ToDto()).ToArray();
    }

    public async Task RevokeAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        var apiKey = await dbContext.ApiKeys
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("API key was not found.");

        apiKey.RevokedAt = dateTimeProvider.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public static string ComputeHash(string secret)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secret));
        return Convert.ToBase64String(bytes);
    }

    private static string GenerateToken(int byteCount)
    {
        var bytes = new byte[byteCount];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private Guid RequireCurrentUser()
    {
        var userId = currentUserContext.UserId;
        if (!currentUserContext.IsAuthenticated || userId == Guid.Empty)
        {
            throw new UnauthorizedAppException("Authentication is required.");
        }

        return userId;
    }

    private static string RequireText(string value, string fieldName, int maxLength)
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
}
