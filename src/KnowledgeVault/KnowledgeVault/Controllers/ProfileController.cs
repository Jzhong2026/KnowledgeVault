using KnowledgeVault.Contracts.ApiKeys;
using KnowledgeVault.Contracts.Auth;
using KnowledgeVault.Contracts.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeVault.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/profile")]
public sealed class ProfileController(
    IAuthProvider authProvider,
    IApiKeyProvider apiKeyProvider) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<UserProfileDto>> GetProfile(CancellationToken cancellationToken)
    {
        return Ok(await authProvider.GetCurrentUserAsync(cancellationToken));
    }

    [HttpPut]
    public async Task<ActionResult<UserProfileDto>> UpdateProfile(
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await authProvider.UpdateNicknameAsync(request, cancellationToken));
    }

    [HttpGet("api-keys")]
    public async Task<ActionResult<IReadOnlyList<ApiKeyDto>>> ListApiKeys(CancellationToken cancellationToken)
    {
        return Ok(await apiKeyProvider.ListAsync(cancellationToken));
    }

    [HttpPost("api-keys")]
    public async Task<ActionResult<ApiKeyCreatedDto>> CreateApiKey(
        CreateApiKeyRequest request,
        CancellationToken cancellationToken)
    {
        var apiKey = await apiKeyProvider.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ListApiKeys), new { id = apiKey.Id }, apiKey);
    }

    [HttpDelete("api-keys/{apiKeyId:guid}")]
    public async Task<IActionResult> RevokeApiKey(Guid apiKeyId, CancellationToken cancellationToken)
    {
        await apiKeyProvider.RevokeAsync(apiKeyId, cancellationToken);
        return NoContent();
    }
}
