namespace KnowledgeVault.Contracts.Chat;

public sealed record ChatRequest(
    string Message,
    Guid? ProjectId,
    IReadOnlyList<ChatHistoryMessage>? History);

public sealed record ChatHistoryMessage(string Role, string Content);

public sealed record ChatCitation(
    string Source,
    string SourceId,
    string Title,
    string Anchor,
    double Score);

public sealed record ChatAnswer(
    string Text,
    IReadOnlyList<ChatCitation> Citations);

public sealed record ReindexStatus(
    bool IsRunning,
    DateTimeOffset? LastRunAt,
    int TotalChunks,
    string? LastError);
