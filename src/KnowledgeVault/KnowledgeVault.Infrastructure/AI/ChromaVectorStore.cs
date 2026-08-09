using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KnowledgeVault.Infrastructure.AI;

/// <summary>
/// ChromaDB HTTP client. Talks to the Chroma v1 API
/// (<c>/api/v1/collections/&lt;name&gt;/...</c>) using only HttpClient. The
/// metadata schema is normalized so that permission filters can be expressed
/// in Chroma <c>where</c> clauses.
/// </summary>
public sealed class ChromaVectorStore : IVectorStore
{
    private readonly HttpClient _http;
    private readonly VectorStoreOptions _options;
    private readonly ILogger<ChromaVectorStore> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ChromaVectorStore(
        HttpClient http,
        IOptions<VectorStoreOptions> options,
        ILogger<ChromaVectorStore> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
        _http.BaseAddress ??= new Uri(_options.Endpoint.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(30);
        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }
    }

    public async Task EnsureCollectionAsync(CancellationToken cancellationToken = default)
    {
        var resp = await _http.GetAsync($"api/v1/collections/{_options.Collection}", cancellationToken);
        if (resp.IsSuccessStatusCode) return;
        if (resp.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            var body = await resp.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Chroma GetCollection failed: {(int)resp.StatusCode} {body}");
        }
        var create = new
        {
            name = _options.Collection,
            metadata = new { distance = _options.Distance }
        };
        var post = await _http.PostAsJsonAsync("api/v1/collections", create, JsonOpts, cancellationToken);
        if (!post.IsSuccessStatusCode)
        {
            var body = await post.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Chroma CreateCollection failed: {(int)post.StatusCode} {body}");
        }
    }

    public async Task UpsertAsync(IReadOnlyList<VectorChunk> chunks, CancellationToken cancellationToken = default)
    {
        if (chunks.Count == 0) return;
        const int batchSize = 100;
        for (var i = 0; i < chunks.Count; i += batchSize)
        {
            var slice = chunks.Skip(i).Take(batchSize).ToArray();
            var payload = new
            {
                ids = slice.Select(c => c.Id),
                embeddings = slice.Select(c => c.Embedding),
                documents = slice.Select(c => c.Text),
                metadatas = slice.Select(c => BuildMetadata(c))
            };
            var resp = await _http.PostAsJsonAsync(
                $"api/v1/collections/{_options.Collection}/upsert",
                payload, JsonOpts, cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException($"Chroma upsert failed: {(int)resp.StatusCode} {body}");
            }
        }
    }

    public async Task DeleteBySourceAsync(VectorSourceType source, string sourceId, CancellationToken cancellationToken = default)
    {
        var where = new
        {
            source = source.ToString(),
            source_id = sourceId
        };
        var resp = await _http.PostAsJsonAsync(
            $"api/v1/collections/{_options.Collection}/delete",
            new { where }, JsonOpts, cancellationToken);
        if (!resp.IsSuccessStatusCode && resp.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            var body = await resp.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Chroma DeleteBySource failed: {Status} {Body}", (int)resp.StatusCode, body);
        }
    }

    public async Task DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        var resp = await _http.PostAsJsonAsync(
            $"api/v1/collections/{_options.Collection}/delete",
            new { where = new { } },
            JsonOpts, cancellationToken);
        if (!resp.IsSuccessStatusCode && resp.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            var body = await resp.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Chroma DeleteAll failed: {(int)resp.StatusCode} {body}");
        }
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(VectorQuery query, CancellationToken cancellationToken = default)
    {
        var where = BuildWhereClause(query);
        var payload = new
        {
            query_embeddings = new[] { query.QueryEmbedding },
            n_results = query.TopK,
            where
        };
        var resp = await _http.PostAsJsonAsync(
            $"api/v1/collections/{_options.Collection}/query",
            payload, JsonOpts, cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Chroma query failed: {(int)resp.StatusCode} {body}");
        }
        var raw = await resp.Content.ReadFromJsonAsync<ChromaQueryResponse>(JsonOpts, cancellationToken);
        if (raw?.Ids is null || raw.Ids.Count == 0) return Array.Empty<VectorSearchResult>();
        var results = new List<VectorSearchResult>(raw.Ids.Count);
        for (var i = 0; i < raw.Ids.Count; i++)
        {
            var id = raw.Ids[i];
            var doc = raw.Documents is { Count: > 0 } ? raw.Documents[i] : string.Empty;
            var score = raw.Distances is { Count: > 0 } ? raw.Distances[i] : 0d;
            var meta = raw.Metadatas is { Count: > 0 } && raw.Metadatas[i] is { } m
                ? ParseMetadata(m) : new Dictionary<string, string>();
            var src = Enum.TryParse<VectorSourceType>(meta.GetValueOrDefault("source", "Document"), out var s) ? s : VectorSourceType.Document;
            var projectId = Guid.TryParse(meta.GetValueOrDefault("project_id"), out var pid) ? pid : (Guid?)null;
            var ownerId = Guid.TryParse(meta.GetValueOrDefault("owner_user_id"), out var oid) ? oid : (Guid?)null;
            results.Add(new VectorSearchResult(
                id, src, meta.GetValueOrDefault("source_id", string.Empty),
                projectId, ownerId, doc ?? string.Empty, meta, score));
        }
        return results;
    }

    private static Dictionary<string, object?> BuildMetadata(VectorChunk c)
    {
        var m = new Dictionary<string, object?>
        {
            ["source"] = c.Source.ToString(),
            ["source_id"] = c.SourceId,
            ["scope"] = c.Scope,
            ["project_id"] = c.ProjectId?.ToString(),
            ["owner_user_id"] = c.OwnerUserId?.ToString(),
            ["folder_id"] = c.FolderId?.ToString(),
            ["document_type"] = c.DocumentType,
            ["revision_number"] = c.RevisionNumber,
            ["status"] = c.Status
        };
        foreach (var kv in c.ExtraMetadata) m[kv.Key] = kv.Value;
        return m;
    }

    private static Dictionary<string, string> ParseMetadata(JsonElement el)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (el.ValueKind != JsonValueKind.Object) return result;
        foreach (var p in el.EnumerateObject())
        {
            result[p.Name] = p.Value.ValueKind switch
            {
                JsonValueKind.String => p.Value.GetString() ?? string.Empty,
                JsonValueKind.Number => p.Value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => p.Value.GetRawText()
            };
        }
        return result;
    }

    private static Dictionary<string, object>? BuildWhereClause(VectorQuery q)
    {
        // Chroma expects a single top-level field for one condition and
        // `$and` / `$or` arrays for multiple. Each conjunct we build is a
        // single {field: predicate} object, so:
        //  - 0 conjuncts => no filter
        //  - 1 conjunct  => unwrap to top-level field
        //  - N conjuncts => wrap in { $and: [...] }
        var conjuncts = new List<Dictionary<string, object>>();
        if (q.AllowedSources is { Count: > 0 })
        {
            conjuncts.Add(new Dictionary<string, object>
            {
                ["source"] = new Dictionary<string, object>
                {
                    ["$in"] = q.AllowedSources.Select(s => s.ToString()).ToArray()
                }
            });
        }
        if (q.AllowedProjectIds is { Count: > 0 })
        {
            conjuncts.Add(new Dictionary<string, object>
            {
                ["project_id"] = new Dictionary<string, object>
                {
                    ["$in"] = q.AllowedProjectIds.Select(g => g.ToString()).ToArray()
                }
            });
        }
        if (q.OwnerUserId.HasValue)
        {
            conjuncts.Add(new Dictionary<string, object>
            {
                ["owner_user_id"] = q.OwnerUserId.Value.ToString()
            });
        }
        if (q.Where is { Count: > 0 })
        {
            foreach (var kv in q.Where)
            {
                conjuncts.Add(new Dictionary<string, object> { [kv.Key] = kv.Value });
            }
        }
        return conjuncts.Count switch
        {
            0 => null,
            1 => conjuncts[0],
            _ => new Dictionary<string, object> { ["$and"] = conjuncts }
        };
    }

    private static Dictionary<string, object>? ToDict(object anon)
    {
        var json = JsonSerializer.Serialize(anon, JsonOpts);
        return JsonSerializer.Deserialize<Dictionary<string, object>>(json, JsonOpts);
    }

    // --- Chroma response DTOs (private) ---

    private sealed class ChromaQueryResponse
    {
        [JsonPropertyName("ids")]
        public List<string>? Ids { get; set; }
        [JsonPropertyName("documents")]
        public List<string?>? Documents { get; set; }
        [JsonPropertyName("distances")]
        public List<double>? Distances { get; set; }
        [JsonPropertyName("metadatas")]
        public List<JsonElement?>? Metadatas { get; set; }
    }
}
