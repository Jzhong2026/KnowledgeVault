using System.Text.RegularExpressions;
using KnowledgeVault.Infrastructure.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KnowledgeVault.Providers.AI;

/// <summary>
/// Two-tier intent classification:
///   1) Fast keyword/regex layer — no LLM call, covers the obvious cases
///      ("find plan", "review status", "memory of").
///   2) Falls back to a single cheap LLM call (default model is
///      <c>gpt-4o-mini</c>) that returns one of the four intent labels.
/// On any LLM failure, the router returns <see cref="IntentKind.GeneralQuestion"/>
/// so the caller can still serve an answer via vector RAG.
/// </summary>
public sealed class IntentRouter : IIntentRouter
{
    private readonly ILLMProvider _llm;
    private readonly LlmOptions _options;
    private readonly ILogger<IntentRouter> _logger;

    // Cached compiled regexes. We deliberately use static readonly fields
    // (instead of [GeneratedRegex] partial methods) so the pattern is
    // trivially debuggable from tests and the behavior is identical
    // whether or not the source generator has run.
    private static readonly Regex PlanPatternInstance = new(
        @"\b(plan|planning|planning review|task breakdown|story plan|story)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ReviewPatternInstance = new(
        @"\b(review|reviews|reviewer|审批|审核|review status|review detail|reviewer comment)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex MemoryPatternInstance = new(
        @"\b(memory|memories|decision|decisions|约定|规范|决定|memoria|project memory)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public IntentRouter(ILLMProvider llm, IOptions<LlmOptions> options, ILogger<IntentRouter> logger)
    {
        _llm = llm;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IntentKind> ClassifyAsync(string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message)) return IntentKind.GeneralQuestion;
        var planHits = PlanPatternInstance.Matches(message).Count;
        var reviewHits = ReviewPatternInstance.Matches(message).Count;
        var memoryHits = MemoryPatternInstance.Matches(message).Count;
        if (planHits + reviewHits + memoryHits > 0)
        {
            // One signal is much stronger than the others -> skip the LLM call.
            var max = Math.Max(Math.Max(planHits, reviewHits), memoryHits);
            if (planHits == max && planHits > reviewHits && planHits > memoryHits) return IntentKind.FindPlan;
            if (reviewHits == max && reviewHits > planHits && reviewHits > memoryHits) return IntentKind.FindReview;
            if (memoryHits == max && memoryHits > planHits && memoryHits > reviewHits) return IntentKind.FindMemory;
        }
        try
        {
            var prompt = new[]
            {
                new ChatMessage("system",
                    "You are an intent classifier for a knowledge-base chatbot. " +
                    "Given the user's question, reply with EXACTLY one of these labels, no punctuation, no explanation:\n" +
                    "FindPlan — about a planning or task-breakdown document for a story/task\n" +
                    "FindReview — about review status, reviewer decisions, review comments\n" +
                    "FindMemory — about project memory / MEMORY.md / decisions / conventions\n" +
                    "GeneralQuestion — anything else"),
                new ChatMessage("user", message)
            };
            var response = await _llm.CompleteAsync(prompt, temperature: 0.0, cancellationToken: cancellationToken);
            var cleaned = response.Trim();
            return cleaned switch
            {
                "FindPlan" => IntentKind.FindPlan,
                "FindReview" => IntentKind.FindReview,
                "FindMemory" => IntentKind.FindMemory,
                _ => IntentKind.GeneralQuestion
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Intent classification LLM call failed; falling back to GeneralQuestion.");
            return IntentKind.GeneralQuestion;
        }
    }
}
