# KnowledgeVault Chatbot

The chatbot lets users ask natural-language questions and get cited answers
backed by the existing knowledge graph: documents, revisions, reviews,
comments, and project memory. It is implemented as a thin OpenAI-compatible
chat layer on top of a ChromaDB vector store.

## Architecture

```
┌────────────────┐   SSE / REST   ┌──────────────────┐
│  Angular UI    │ ──────────────▶│  ChatController  │
│  ChatPanel     │                │  (HTTP + MCP)    │
└────────────────┘                └────────┬─────────┘
                                           │
                              ┌────────────▼─────────────┐
                              │      ChatService         │
                              │  ┌────────────────────┐  │
                              │  │ 1. IntentRouter    │  │  (keyword + cheap LLM)
                              │  │ 2. Retriever       │  │  (structured / vector)
                              │  │ 3. ChatPromptBuild │  │  (build LLM prompt)
                              │  │ 4. LLMProvider     │  │  (OpenAI-compatible)
                              │  └────────────────────┘  │
                              └────────────┬─────────────┘
                                           │
              ┌────────────────────────────┼────────────────────────────┐
              ▼                            ▼                            ▼
      ┌──────────────┐            ┌────────────────┐            ┌────────────────┐
      │ Embedding    │            │  ChromaDB      │            │  OpenAI-       │
      │ Provider     │            │  (vector store)│            │  compatible    │
      └──────────────┘            └────────────────┘            │  LLM endpoint  │
                                                                 └────────────────┘
```

## Configuration

Edit `appsettings.json` (or override per-env):

```json
"Llm": {
  "BaseUrl": "https://api.openai.com/v1",
  "ApiKey": "${KV_LLM_API_KEY}",
  "ChatModel": "gpt-4o-mini",
  "EmbeddingModel": "text-embedding-3-small"
},
"VectorStore": {
  "Provider": "Chroma",
  "Endpoint": "http://localhost:8000",
  "Collection": "knowledge_vault_chunks"
}
```

For Docker, set `KV_LLM_API_KEY` in the host environment. The compose
file also starts a ChromaDB sidecar on `chromadb:8000`.

## Local development

```bash
# 1. Start ChromaDB
docker run -d -p 8000:8000 --name kv-chroma chromadb/chroma:0.5.20

# 2. Run the API (auto-migrates the SQLite DB)
export KV_LLM_API_KEY=sk-...
cd src/KnowledgeVault
dotnet run --project KnowledgeVault

# 3. Trigger a one-time full reindex
curl -X POST http://localhost:5030/api/chat/admin/reindex -H "Authorization: Bearer $TOKEN"

# 4. Ask a question (non-streaming)
curl -X POST http://localhost:5030/api/chat/messages \
     -H "Content-Type: application/json" -H "Authorization: Bearer $TOKEN" \
     -d '{"message":"find the plan for the auth refactor","projectId":null,"history":null}'
```

## What gets indexed

| Source                  | Chunked? | Visible to whom           |
|-------------------------|----------|---------------------------|
| KnowledgeItem (current) | yes      | per-project members / personal owner |
| KnowledgeItemRevision   | yes      | same as the item it belongs to |
| DocumentRevisionReview  | yes      | same as the item it belongs to |
| KnowledgeItemComment    | yes      | same as the item it belongs to |
| ProjectMemoryCandidate  | accepted | per-project members |

The full reindex walks every row above, chunks each Markdown body via
`MarkdownChunker`, embeds the chunks (batched, 32 at a time), and upserts
them to ChromaDB with permission metadata (`project_id`, `owner_user_id`,
`scope`). On the read path, `Retriever` adds a `where` clause to every
vector query so the user can never see a chunk they are not allowed to.

## MCP tools

`ask_knowledge_vault(message, projectId?)` — non-streaming answer with citations.
`reindex_knowledge_vault` — trigger a full reindex (admin scope).

## Intents

`IntentRouter` is a two-tier classifier:

1. Keyword/regex pass — covers "find plan", "review status", "memory of".
2. On miss, a single LLM call returns one of `FindPlan | FindReview | FindMemory | GeneralQuestion`.

For structured intents the `Retriever` queries the existing providers
directly (`IDocumentProvider`, `IDocumentReviewProvider`,
`IProjectMemoryProvider`) so the answer is always grounded in the live
database. For `GeneralQuestion` it falls back to a vector search.

## What this PR does NOT do

- Incremental indexing. Reindex is full only; runs in a background task.
- Streaming on the MCP tool (MCP tools return a single response, so they use
  `AskAsync`). The HTTP `/api/chat/messages/stream` endpoint streams SSE.
- Multilingual embedding tuning. The default model handles English and
  Chinese with reasonable quality out of the box.
- Memory of multi-turn conversations beyond the last 6 messages. Long-term
  conversation state is left for a future iteration.
