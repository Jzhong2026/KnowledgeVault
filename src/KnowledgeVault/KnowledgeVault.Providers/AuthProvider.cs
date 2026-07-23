using KnowledgeVault.Contracts.Auth;
using KnowledgeVault.Contracts.Providers;
using KnowledgeVault.Contracts.Security;
using KnowledgeVault.DataAccess;
using KnowledgeVault.Domain.Entities;
using KnowledgeVault.Infrastructure.Auth;
using KnowledgeVault.Infrastructure.Exceptions;
using KnowledgeVault.Infrastructure.Security;
using KnowledgeVault.Infrastructure.Text;
using KnowledgeVault.Infrastructure.Time;
using KnowledgeVault.Providers.Mapping;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeVault.Providers;

public sealed class AuthProvider(
    KnowledgeVaultDbContext dbContext,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator jwtTokenGenerator,
    IDateTimeProvider dateTimeProvider,
    ICurrentUserContext currentUserContext) : IAuthProvider
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var userName = RequestText.Require(request.UserName, "User name", 64);
        var email = RequestText.Require(request.Email, "Email", 256);
        var password = RequirePassword(request.Password);
        var normalizedUserName = TextNormalizer.NormalizeName(userName);
        var normalizedEmail = TextNormalizer.NormalizeName(email);

        var exists = await dbContext.Users.AnyAsync(
            x => x.NormalizedUserName == normalizedUserName || x.NormalizedEmail == normalizedEmail,
            cancellationToken);

        if (exists)
        {
            throw new ConflictException("User name or email already exists.");
        }

        var passwordResult = passwordHasher.Hash(password);
        var now = dateTimeProvider.UtcNow;
        var user = new User
        {
            UserName = userName,
            NormalizedUserName = normalizedUserName,
            Email = email,
            NormalizedEmail = normalizedEmail,
            PasswordHash = passwordResult.Hash,
            PasswordSalt = passwordResult.Salt,
            CreatedAt = now
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreateAuthResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var userNameOrEmail = RequestText.Require(request.UserNameOrEmail, "User name or email", 256);
        // Login accepts existing credentials independently of the current
        // registration policy so imported/local accounts remain usable.
        var password = RequestText.Require(request.Password, "Password", 512);
        var normalized = TextNormalizer.NormalizeName(userNameOrEmail);

        var user = await dbContext.Users.FirstOrDefaultAsync(
            x => x.NormalizedUserName == normalized || x.NormalizedEmail == normalized,
            cancellationToken);

        if (user is null || !passwordHasher.Verify(password, user.PasswordHash, user.PasswordSalt))
        {
            throw new UnauthorizedAppException("Invalid user name or password.");
        }

        user.LastLoginAt = dateTimeProvider.UtcNow;
        user.UpdatedAt = dateTimeProvider.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreateAuthResponse(user);
    }

    public async Task<UserProfileDto> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var userId = currentUserContext.RequireUserId();
        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new NotFoundException("User was not found.");

        return user.ToProfileDto();
    }

    public async Task<UserProfileDto> UpdateNicknameAsync(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUserContext.RequireUserId();
        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new NotFoundException("User was not found.");

        var nickname = string.IsNullOrWhiteSpace(request.Nickname) ? null : request.Nickname.Trim();
        if (nickname is not null && nickname.Length > 64)
        {
            throw new ValidationException("Nickname must be 64 characters or fewer.");
        }

        user.Nickname = nickname;
        user.UpdatedAt = dateTimeProvider.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return user.ToProfileDto();
    }

    private AuthResponse CreateAuthResponse(User user)
    {
        var token = jwtTokenGenerator.GenerateToken(user.Id, user.UserName, user.Email);
        return new AuthResponse(token.AccessToken, token.ExpiresAt, user.ToProfileDto());
    }

    private static string RequirePassword(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 8)
        {
            throw new ValidationException("Password must be at least 8 characters.");
        }

        return value;
    }
}
