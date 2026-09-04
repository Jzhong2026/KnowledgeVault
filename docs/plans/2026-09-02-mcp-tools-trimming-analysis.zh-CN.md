# MCP 工具精简分析

> 日期：2026-09-02
> 对象：`KnowledgeVault` HTTP MCP 端点（`/mcp`，SDK `ModelContextProtocol.AspNetCore` 1.4.1）
> 性质：分析 + 建议；balanced 工具档与 document 描述精简已实现

## 1. 结论

当前 MCP 暴露 **38 个 tool、3 个 resource、1 个 prompt**。其中有 **6 个 tool 与已有 resource 或其他 tool 完全等价**，**3 组 tool 可合并**。保守精简可到 **29 个（-24%）**，激进精简可到 **24 个（-37%）**，只保留 Agent 主路径可压到 **18–20 个**。

工具清单本身约 **9074 字符**的 `Description` 文本，加上 JSON schema 骨架后，`tools/list` 的常驻开销约 **5–6 千 token**。但真正的大头不在 KnowledgeVault：IDE 里挂的 `agentboard` server 有 100 个工具，是 KnowledgeVault 的近 3 倍。**先用「按类开关 + 客户端禁用不用的 server」解决，再动刀删工具，性价比最高。**

## 2. 现状

### 2.1 分布

| 文件 | 行数 | tool | resource | prompt | 描述字符 |
|---|---|---|---|---|---|
| `Mcp/DocumentMcpTools.cs` | 336 | 12 | 0 | 0 | 4371 |
| `Mcp/DocumentReviewMcpTools.cs` | 122 | 5 | 0 | 0 | 992 |
| `Mcp/ProjectMemoryMcpTools.cs` | 120 | 5 | 0 | 0 | 789 |
| `Mcp/CommentMcpTools.cs` | 134 | 6 | 0 | 0 | 664 |
| `Mcp/ChatMcpTools.cs` | 60 | 2 | 0 | 0 | 539 |
| `Mcp/RevisionMcpTools.cs` | 71 | 3 | 0 | 0 | 488 |
| `Mcp/ProjectMcpTools.cs` | 70 | 3 | 0 | 0 | 370 |
| `Mcp/FolderMcpTools.cs` | 42 | 1 | 0 | 0 | 350 |
| `Mcp/KnowledgeVaultMcpResources.cs` | 89 | 0 | 3 | 0 | 324 |
| `Mcp/CategoryMcpTools.cs` | 27 | 1 | 0 | 0 | 83 |
| `Mcp/KnowledgeVaultMcpPrompts.cs` | 47 | 0 | 0 | 1 | 104 |
| **合计** | | **38** | **3** | **1** | **9074** |

`DocumentMcpTools.cs` 一个文件占掉全部描述文本的 **48%**，是描述最啰嗦的部分（`get_document_content_range` 单工具：描述 268 字符 + 8 个参数描述约 600 字符）。

### 2.2 Agent 主路径

`docs/plans/2026-09-02-mcp-large-document-io.zh-CN.md` 第 6 节定义了理想工作流，只有 5 步：

```text
list/search 定位 → get_knowledge_item 拿头+大纲 → search_in_document / get_document_content_range
→ apply_document_patch → update_document_metadata
```

也就是说，38 个工具里只有 **7 个** 在高频主路径上，其余 31 个是低频或流程性工具。

## 3. 冗余分析

### 3.1 完全等价（可直接删，有替代品）

| # | tool | 位置 | 等价替代 |
|---|---|---|---|
| 1 | `get_document_outline` | `DocumentMcpTools.cs:102-122` | `get_knowledge_item` 的描述已明写 "content hash, and heading outline"，返回值就是大纲 |
| 2 | `get_knowledge_item` | `DocumentMcpTools.cs:84-100` | resource `knowledge://{id}`（`KnowledgeVaultMcpResources.cs:15-38`），同样返回 metadata + outline + hash |
| 3 | `get_document_revision` | `RevisionMcpTools.cs:34-50` | resource `revision://{documentId}/{revisionNumber}`（`KnowledgeVaultMcpResources.cs:61-88`） |
| 4 | `get_project_memory` | `ProjectMemoryMcpTools.cs:17-31` | resource `project-memory://{projectId}`（`KnowledgeVaultMcpResources.cs:40-59`） |
| 5 | `move_document` | `DocumentMcpTools.cs:317-335` | `update_document_metadata(folderId=...)`，`DocumentMcpBinding.cs:47-48` 的 `FolderId` / `UpdateFolder` 分支语义完全一致 |
| 6 | `list_categories` | `CategoryMcpTools.cs:14-26` | 唯一用途是给 `create_document` / `update_document` 的 `categoryId` 找值，而这两个参数都是可选，实际极少用 |

### 3.2 可合并（功能重叠、底层同一实现）

| # | 合并对象 | 依据 |
|---|---|---|
| 7 | `add_revision_comment` + `reply_to_revision_comment` | `CommentMcpTools.cs:37-63`，两者共用私有 `AddCommentAsync`，只差 `parentCommentId` 是否为 null。合成一个 `parentCommentId?` 即可 |
| 8 | `update_revision_comment` + `resolve_revision_comment` | `CommentMcpTools.cs:65-99`，同一个 `ICommentProvider` 的 update/resolve，可合成 `content?` + `isResolved?` |
| 9 | `search_knowledge_items` + `list_project_documents` | `DocumentMcpTools.cs:18-47` 与 `:51-82`，两者都是 `IDocumentProvider.ListAsync(new DocumentQuery(...))`，差别仅在 `scope` 与是否有 type/topic/status 过滤。合成一个「projectId 可选 + 过滤器全可选」的 `list_documents` |

### 3.3 低频（激进档再删 5 个）

| # | tool | 位置 | 理由 |
|---|---|---|---|
| 10 | `reindex_knowledge_vault` | `ChatMcpTools.cs:49-59` | 运维操作，需要 `documents:write` scope，正常 Agent 工作流不会触发；REST 侧已有入口 |
| 11 | `cancel_document_review` | `DocumentReviewMcpTools.cs:107-121` | 评审流程的分支操作，频率远低于 request/submit |
| 12 | `delete_revision_comment` | `CommentMcpTools.cs:101-114` | 评论删除在协作场景极少从 Agent 发起 |
| 13 | `get_revision_diff` | `RevisionMcpTools.cs:52-71` | `get_document_review_context` 已内含 unified diff |
| 14 | `get_project` | `ProjectMcpTools.cs:34-53` | `list_projects` 已覆盖项目字段，唯一增量是 members，可用 `list_project_members` |

### 3.4 建议保留（看似可删但实际不能动）

- `create_folder`（`FolderMcpTools.cs:16-41`）：唯一建目录手段，删了文档树就断了。
- `update_document`（`DocumentMcpTools.cs:227-265`）：方案文档明确「保留为全量兜底」，改动密度接近通篇重写时 Agent 只能走它。
- `list_project_members`（`ProjectMcpTools.cs:55-...`）：`request_document_review` 挑 reviewer 的唯一来源，删了评审就残废。
- `accept_project_memory_candidate` / `cancel_project_memory_candidate`：MEMORY.md 审批闭环，是 KnowledgeVault 的差异化能力。

## 4. 精简方案

### 档位对比

| 档位 | 剩余工具 | 减少 | 内容 |
|---|---|---|---|
| 保守 | 29 | -9 | 删 3.1 全部 6 个 + 合并 3.2 三组 |
| 激进 | 24 | -14 | 保守 + 删 3.3 全部 5 个 |
| 极简 | 18–20 | -18~20 | 激进 + 评论只留 list/add、revision 只留 list |

### 落地手段（按侵入性从低到高）

**手段 A：客户端侧禁用不用的 server（零代码改动，收益最大）**
IDE 当前挂了三个 MCP server。`agentboard`（约 100 个工具）是 KnowledgeVault（38 个）的近 3 倍，`localmcptools` 约 30 个。合计约 170 个工具。不用 `agentboard` 时直接关掉，等于一次省掉 60% 的工具预算。

**手段 B：按类开关（改 `Program.cs` 约 10 行，零 SDK 内部依赖）**
当前 `Program.cs:121-133` 是逐个 `WithTools<T>()` 硬编码注册。把每个注册包一层配置判断即可按部署/租户裁剪：

```csharp
var mcp = builder.Services.AddMcpServer().WithHttpTransport();
if (mcpConfig.Tools.Core)   { mcp.WithTools<ProjectMcpTools>().WithTools<DocumentMcpTools>().WithTools<FolderMcpTools>(); }
if (mcpConfig.Tools.Review) { mcp.WithTools<RevisionMcpTools>().WithTools<CommentMcpTools>().WithTools<DocumentReviewMcpTools>(); }
if (mcpConfig.Tools.Memory) { mcp.WithTools<ProjectMemoryMcpTools>(); }
if (mcpConfig.Tools.Chat)   { mcp.WithTools<ChatMcpTools>(); }
```

粒度是「类」（1–12 个工具），足够把评论/评审/内存这几块整体关掉。

**手段 C：单工具粒度过滤（需实机验证）**
SDK 1.4.1 的 XML 文档确认 `McpServerOptions.ToolCollection` 是「可被设置的服务工具集合」，且 `tools/list` 的输出 = `ToolCollection` 全量 + 自定义 `ListToolsHandler` 的额外输出。因此要真正过滤，必须从 `ToolCollection` 移除，用自定义 handler 只能「加」不能「减」。可行做法是 `services.AddOptions<McpServerOptions>().PostConfigure(...)` 里按自定义 attribute 移除条目 —— 但 `ToolCollection` 是否可变（是否实现 `IList`）本轮未能在本机反射验证（PowerShell 无法加载 net9.0 程序集），动手前需先写个小测试确认。

注意：SDK 明确 `tools/list` 支持 `cursor` 分页，但主流客户端仍一次性拉全量，不能指望分页省 token。

**手段 D：描述瘦身（不改工具数，立竿见影）**
9074 字符里 `DocumentMcpTools.cs` 占 4371。把长描述压到 1/2、`get_document_content_range` 的 8 个参数描述精简，整体可省约 1500–2000 字符（对应完整 schema 约 1 千 token）。

本次先落地低风险的 balanced 配置：默认从 `tools/list` 隐藏 `get_document_outline`、`move_document`、`list_categories`、`reindex_knowledge_vault` 四个低频或重复工具，核心 document 查询、局部读取、局部 patch 与精简写回保持不变。需要兼容旧客户端时，将 `Mcp:Tools:Profile` 设为 `full` 即可恢复全部工具。

## 5. 风险

- **删工具是破坏性契约。** 已发出的 API key 客户端调用被删工具会直接报「tool not found」。建议先上手段 B 的开关灰度，确认无客户端依赖后再删代码。
- **依赖 resource 的删除是有条件的。** 第 3.1 节的 #2/#3/#4 用 resource 替代 tool，前提是客户端支持 MCP resources。并非所有客户端都支持（部分只支持 tools）。**建议这三步只做一半：先删 `get_document_outline`（纯重复，无风险），`get_knowledge_item` / `get_project_memory` 在确认客户端支持 resource 后再删，或改为把它们保留、只删另两个。**
- **`move_document` 的删除安全性高。** `update_document_metadata.folderId` 的参数描述（`DocumentMcpTools.cs:300`）已经写明「pass empty to move to the workspace root」，与 `move_document` 逐字等价，且 `DocumentMcpBinding.BindMetadata` 确有 `UpdateFolder` 分支。
- **`src/KnowledgeVault/KnowledgeVault.Api.Tests/McpStartupProbeTests.cs` 是临时探针**（注释写着 TEMPORARY probe，用 `Assert.Fail` 故意失败）。它目前是 untracked 文件，一旦被纳入编译，`dotnet test` 会直接失败。建议确认用途后删除，否则后续任何验证都会被它干扰。

## 6. 建议的执行顺序

1. 先做手段 A（关掉不用的 server）与手段 D（描述瘦身），零风险、立即见效。
2. 再上手段 B（按类开关），把 `Comment` / `Review` / `Memory` / `Chat` 变成可配置的，观察一段时间实际调用。
3. 最后执行删除：先删 `get_document_outline`、`move_document`、`list_categories`、`reindex_knowledge_vault` 这 4 个无争议项（38 → 34）。
4. 再根据调用数据决定是否合并 3.2 三组、删除 3.3 五项。
5. 顺手处理 `McpStartupProbeTests.cs`。
