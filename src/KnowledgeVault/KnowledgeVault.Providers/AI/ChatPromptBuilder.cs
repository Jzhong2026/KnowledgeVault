using KnowledgeVault.Contracts.Chat;
using KnowledgeVault.Infrastructure.AI;

namespace KnowledgeVault.Providers.AI;

/// <summary>
/// Composes the system + user messages sent to the chat LLM. The system
/// prompt instructs the model to answer ONLY from the supplied retrieval
/// context, to inline citation markers like <c>[1]</c>, and to refuse when
/// the answer is not present in the context.
/// </summary>
public static class ChatPromptBuilder
{
    private const string SystemPrompt = """
        You are a knowledge-base assistant. You answer the user's question
        using ONLY the context provided below. If the context does not contain
        enough information to answer, say so explicitly and suggest what the
        user could search for next.

        Cite every claim with the matching bracket number, e.g. [1] or [2].
        The numbers correspond to the order of the "Retrieved context" list
        at the end of this prompt. Be concise: 2-6 short sentences. Use the
        user's language when possible.

        """;

    public static IReadOnlyList<ChatMessage> Build(
        string userMessage,
        IReadOnlyList<ChatHistoryMessage> history,
        IReadOnlyList<RetrievalResult> retrieval,
        IntentKind intent)
    {
        var messages = new List<ChatMessage>
        {
            new("system", SystemPrompt + $"Detected intent: {intent}.")
        };
        if (history is { Count: > 0 })
        {
            foreach (var h in history.TakeLast(6))
            {
                messages.Add(new ChatMessage(h.Role, h.Content));
            }
        }
        messages.Add(new ChatMessage("user", BuildUserTurn(userMessage, retrieval)));
        return messages;
    }

    private static string BuildUserTurn(string userMessage, IReadOnlyList<RetrievalResult> retrieval)
    {
        if (retrieval.Count == 0)
        {
            return "Question: " + userMessage + "\n\nRetrieved context: (none — answer from general knowledge only if safe, otherwise say you don't know).";
        }
        var sb = new System.Text.StringBuilder();
        sb.Append("Question: ").AppendLine(userMessage);
        sb.AppendLine();
        sb.AppendLine("Retrieved context:");
        for (var i = 0; i < retrieval.Count; i++)
        {
            var r = retrieval[i];
            sb.Append('[').Append(i + 1).Append("] ");
            sb.Append(r.Title).Append("  (");
            sb.Append(r.Source).Append(", id=").Append(r.SourceId);
            sb.Append(", anchor=").Append(r.Anchor);
            sb.AppendLine(")");
            sb.AppendLine(r.Text.Length > 1500 ? r.Text[..1500] : r.Text);
            sb.AppendLine("---");
        }
        return sb.ToString();
    }

    /// <summary>Maps bracket markers in the LLM output to ChatCitation objects.</summary>
    public static IReadOnlyList<ChatCitation> ExtractCitations(
        string answer,
        IReadOnlyList<RetrievalResult> retrieval)
    {
        if (string.IsNullOrEmpty(answer) || retrieval.Count == 0) return Array.Empty<ChatCitation>();
        var cited = new HashSet<int>();
        for (var i = 0; i < answer.Length - 1; i++)
        {
            if (answer[i] != '[') continue;
            var close = answer.IndexOf(']', i + 1);
            if (close < 0) continue;
            var inner = answer.Substring(i + 1, close - i - 1);
            if (int.TryParse(inner, out var n) && n >= 1 && n <= retrieval.Count)
            {
                cited.Add(n - 1);
            }
        }
        var citations = new List<ChatCitation>(cited.Count);
        foreach (var idx in cited.OrderBy(x => x))
        {
            var r = retrieval[idx];
            citations.Add(new ChatCitation(
                r.Source.ToString(),
                r.SourceId,
                r.Title,
                r.Anchor,
                r.Score));
        }
        return citations;
    }
}
