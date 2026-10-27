namespace KnowledgeVault.Api.Mcp;

public static class McpServerInstructions
{
    public const string Text =
        "KnowledgeVault returns Markdown documents as head + outline; list and search never include bodies. " +
        "Read a document with get_knowledge_item, then call get_document_content_range (max 24000 characters per call) " +
        "for body text. Always read the current revision first and pass its number as expectedRevisionNumber; " +
        "on a revision conflict, re-read and retry. Prefer apply_document_patch over update_document so only changed " +
        "text is sent. Use get_project_memory or the project-memory://{projectId} resource for shared project context, " +
        "and propose_project_memory_update when a change needs review.";
}
