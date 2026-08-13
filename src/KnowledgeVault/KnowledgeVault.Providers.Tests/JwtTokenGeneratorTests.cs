using KnowledgeVault.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Xunit;

namespace KnowledgeVault.Providers.Tests;

public sealed class JwtTokenGeneratorTests
{
    [Fact]
    public void Default_session_expires_after_ninety_days()
    {
        var clock = new FakeClock();
        var options = Options.Create(new JwtOptions
        {
            SigningKey = "test-signing-key-that-is-at-least-32-bytes-long"
        });
        var generator = new JwtTokenGenerator(options, clock);

        var token = generator.GenerateToken(Guid.NewGuid(), "user", "user@example.test");

        Assert.Equal(clock.UtcNow.AddDays(90), token.ExpiresAt);
    }
}
