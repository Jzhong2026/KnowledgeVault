# MCP 大文档局部读写方案

> 文档状态：已定案并实现  
> 日期：2026-09-02  
> 范围：KnowledgeVault HTTP MCP 的文档读/写契约；不改前端编辑器，不改 revision 存储模型

## 1. 决定

当前 MCP 是「整篇快照 API」：读必拿全文，写必交全文，成功后再回吐全文。一次局部修改会把正文搬运约 `3T–4T`（T = 正文 token），而且模型会重写未改动的部分。

**采用的组合：**

```text
按需读取片段 + 服务端应用 patch + 写入后只回精简确认
```

`get_knowledge_item` **永远不返回正文**（小文档也不回）。没有「再要一次全文」的后门；必须读正文时用 `get_document_content_range` / `search_in_document`。

三份分析的取舍：

| 来源 | 采纳 | 不采纳 / 降级 |
|---|---|---|
| Codex | 精简写回、metadata MCP、outline + range、服务端 patch、去掉更新时加载全部 revision | 把 JSON 缩进当成次要开销（中文 `\uXXXX` 不是次要的） |
| hy4 | 修 `McpJson` 编码器、内容型工具 Markdown 直出、HTTP gzip 顺手开、时间在模型输出 | `*_from_file` 不做通用 MCP 主路径 |
| 本轮 brainstorm | 一次多 hunk 原子提交、review 走 diff、小文档仍允许整篇 | 章节一等存储、版本差量存储、本地 checkout |

## 2. 非目标

本方案不做：

- 服务端按本地绝对路径读盘（`create/update_document_from_file`）。只在 API 与 Agent 同机时成立，且是任意文件读原语。
- 把文档拆成 DB 里的 section 树，或 revision 改存 diff。那是存储优化，不减 MCP token。
- Cursor 专用 checkout / checkin。
- 打开 `UseStructuredContent` 把同一份正文再复制一份。
- 让模型生成 gzip；也不把 gzip 包塞进 tool 文本。HTTP `Content-Encoding` 可以开，与契约无关。

## 3. 现状里必须对准的事实

- `get_knowledge_item` / `create_document` / `update_document` 都搬运完整 `Content`。写成功走 `ReloadAsync`，正文必然再进 tool result。
- `list` / `search` / `list_folder_contents` 已是 summary，保持不变。
- REST 已有 `PATCH .../metadata` 和 `UpdateMetadataAsync`，MCP 未暴露。注意：标题和摘要在 revision 上，**metadata 改不了标题**。
- `UpdateAsync` 使用 `.Include(x => x.Revisions)`。`AdvanceToRevision` 只改 `CurrentRevisionId/Number`，不需要整表历史；这是「大文档 × 多版本」的服务端内存浪费，与 token 无关但要一并修。
- `McpJson` 使用 `JsonSerializerDefaults.Web` + `WriteIndented`。非 ASCII 变成字面 `\uXXXX`，换行变成字面 `\n`。SDK 1.4.1 把 `string` 放进 `TextContentBlock`，客户端只解 JSON-RPC 一层。中文文档这条路径可多耗约 40% token，且破坏按行定位。
- `get_project_memory` 的 `FormatMemory` 和 MCP resource 已经是 Markdown 直出，内容型工具应对齐它们。
- `MarkdownChunker` 的 1500 字符切片只服务向量检索。MCP 大纲按 **Markdown 标题** 切，不复用检索 chunk 大小。
- `get_document_review_context` 打包当前文档 + 目标 revision + 上一 revision，约两份全文。

## 4. 契约

### 4.1 精简写回（所有写工具）

新建 MCP 写回 DTO，不再返回 `KnowledgeItemDto.Content`：

```text
documentId
currentRevisionNumber
title
status
contentLength
contentHash    # SHA-256 hex of UTF-8 body
changeNote?
```

适用于：`create_document`、`update_document`、`move_document`、`apply_document_patch`、`update_document_metadata`。

### 4.2 读取

**`get_knowledge_item`**

- 始终返回短头：id、title、revision、status、contentLength、contentHash、大纲。
- **永不返回正文**，无论文档多小。
- 没有「再要一次全文」的后门。必须读正文时用 range 分页或 grep。

**`get_document_outline`**

按 ATX 标题（`#`–`######`）返回树：`level`、`heading`、`startLine`、`endLine`、`charOffset`、`charLength`。标题重复时带 `occurrence`（1-based）。无标题则单根节点覆盖全文。

**`get_document_content_range`**

三选一，单次最多 `24_000` 字符：

| 模式 | 参数 |
|---|---|
| 标题 | `heading` + 可选 `occurrence` |
| 行 | `startLine` + `lineCount` |
| 字符 | `offset` + `limit` |

返回：所选 Markdown、实际范围、contentHash（**当前全文**的 hash，便于随后 patch）、currentRevisionNumber。超限则截断并标明。

**`search_in_document`**

在当前正文上查找。`pattern` 默认字面量，`isRegex=true` 时走 .NET 正则（超时/复杂度限制）。最多 20 条 hit，每条带 `line`、`contextLines`（默认 2，最大 8）。

**`get_document_revision`**

与 `get_knowledge_item` 同一套阈值和 range 规则；默认不倾倒历史全文。

**`get_revision_diff`**

`fromRevision` → `toRevision` 的 unified diff。默认上下文 3 行。

**`get_document_review_context`**

改为：文档元数据 + 目标/上一 revision 的 diff + 评论 + 评审记录。不再内嵌两份正文。

### 4.3 写入

**`update_document_metadata`**

直接调用现有 `UpdateMetadataAsync`。改 status / category / tags / topic / folder。不建 revision，不传正文。

**`update_document`**

保留为全量兜底。变化：

- MCP 层 `content` 改为可选：省略则服务端复制当前正文再建 revision（只改 title / summary / changeNote 时不再搬运 T）。
- 成功只回精简确认。
- 工具描述写明：局部改文优先 `apply_document_patch`。

**`apply_document_patch`**

主路径。一次调用、一个新 revision、原子应用。

```text
documentId
expectedRevisionNumber
changeNote?
patches: [{ oldText, newText, replaceAll?: false }]
```

规则：

- `expectedRevisionNumber` 必须匹配，否则 409，带上 currentRevisionNumber。
- `oldText` 在应用前的文档上：0 次命中 → 失败，返回附近原文；>1 次且 `replaceAll` 不为 true → 失败。
- 多条 patch 按文档位置从后往前应用，避免位移；区间重叠 → 整单失败，文档不变。
- 不使用行号当主键，不使用片段 hash（revision 已不可变，乐观锁足够）。
- 成功回精简确认 + `appliedCount`。

分布小改（同一文案多处 / 各处改法不同）都走这一个工具，不要 N 次 `update_document`。

改动密度极高（接近通篇重写）时，Agent 仍可用 `update_document` 提交全文。服务端不做 30% 硬门，避免误伤。

### 4.4 编码与传输

- `McpJson`：`JavaScriptEncoder.UnsafeRelaxedJsonEscaping`，`WriteIndented = false`。列表/搜索等结构化结果继续 JSON，但中文不再变成 `\uXXXX`。
- 内容型读取返回普通文本，不把 Markdown 再包进 JSON 字符串。
- `Program.cs` 增加 `AddResponseCompression` / `UseResponseCompression`。这只减 HTTP 字节，不减 token。
- 不在 tool 参数里增加 gzip 字段。

### 4.5 服务端卫生

`DocumentProvider.UpdateAsync` 去掉 `.Include(x => x.Revisions)`。添加新 revision 继续走 `dbContext.KnowledgeItemRevisions.Add`。

## 5. 实施波次

### Wave 0 — 表示形式与写回（先做，不改领域模型）

1. 修 `McpJson`；HTTP 压缩。
2. 写工具改为精简确认；`move_document` 不再 `GetForMcpAsync` 回全文。
3. `get_knowledge_item` / `get_document_revision` 改为 Markdown 直出 + 大文档截断（截断阶段可先给预览，outline 在 Wave 1 接上）。
4. 暴露 `update_document_metadata`；`update_document` 的 content 改为可选。
5. 去掉更新路径的 `Include(Revisions)`。

验收：中文样本序列化后含真中文、不含 `\u7F16`；创建/更新响应无 `content` 字段；只改 status 不传正文；带 13 个历史版本的更新不再把历史正文载入跟踪集合。

### Wave 1 — 局部读 + 局部写

1. `get_document_outline`、`get_document_content_range`、`search_in_document`。
2. `apply_document_patch`（多 hunk、原子、`replaceAll`）。
3. `get_revision_diff`；`get_document_review_context` 改用 diff。
4. 大文档 `get_knowledge_item` 附带大纲。
5. Provider/MCP 集成测试：截断、range、grep、patch 成功/不唯一/重叠/409、metadata 不碰正文。
6. 测试里断言请求/响应体不含完整大正文（按 `contentLength` 阈值）。

验收：对一篇 >16KB 中文文档，「改两处不相邻段落」的工具链不出现完整正文；patch 约 200 token 量级；失败信息足够让 Agent 只重读失败点。

### Wave 2 — 明确不做，除非另开需求

- `*_from_file` + 路径白名单  
- `replace_document_section`（可用 outline + patch 组合）  
- 版本差量存储、增量重嵌  
- 服务端 `summarize_document`

## 6. Agent 工作流（实现后应变成这样）

1. `list` / `search` 定位文档。  
2. `get_knowledge_item` 拿头和大纲（不含正文）。  
3. `search_in_document` 或 `get_document_content_range` 取要改的片段。  
4. 一次 `apply_document_patch` 提交所有 hunk。  
5. 只看精简确认里的 revision 和 hash。需要核对再 range 读改过的节。  
6. 只改状态/分类走 `update_document_metadata`。只改标题走 `update_document` 且省略 content。

## 7. 风险

- 改变 `get_knowledge_item` 对大文档的返回是破坏性契约。用工具描述写清楚，并靠截断硬限制，不能只靠文案。
- `oldText` 太短会误匹配。失败时返回附近原文，让 Agent 加长上下文再试。
- 无标题的大文档只能靠 range / grep，不能靠 heading。
- MCP 往返在本地很便宜，但每个 turn 仍要付工具 schema。Wave 1 只加必要工具，描述保持短。
