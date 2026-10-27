# MCP 客户端体验优化：工具注解 / 服务端指令 / 调用遥测

> 日期：2026-10-27
> 对象：`KnowledgeVault` HTTP MCP 端点（`/mcp`，SDK `ModelContextProtocol.AspNetCore` 1.4.1）
> 性质：已实现（工具注解、ServerInstructions、CallTool 遥测）

## 1. 背景与结论

`2026-09-02` 的两轮优化已经解决了主要的上下文开销问题：紧凑 JSON（`WriteIndented = false`）、分片读取（`get_document_content_range`，单次上限 24000 字符）、列表/搜索只返回元数据、写操作只回 Ack + 内容哈希、balanced 工具档默认隐藏 4 个低频工具。

这个版本补齐的是**客户端协议层**的信息缺失：

| 维度 | 优化前 | 优化后 |
|---|---|---|
| 工具注解 | 38 个工具全部无 `readOnlyHint` / `destructiveHint` | 20 个只读工具显式声明 read-only、非破坏 |
| 会话级指引 | 无 `ServerInstructions` | 一条约 600 字符的主路径说明，随 initialize 下发一次 |
| 调用观测 | 只有 HTTP 级 Serilog 请求日志 | 每个 tool 调用记录名称、耗时、参数个数、返回字符数、kid、成败 |

结论：这三项不改业务逻辑，但直接影响 Agent 的**工具选择正确率**、**审批噪音**和**后续优化依据**，性价比高于继续删工具。

## 2. 技术选型

### 2.1 工具注解

MCP 规范中 `readOnlyHint` / `destructiveHint` / `idempotentHint` / `openWorldHint` 是给客户端做审批策略和风险提示的元数据。Qoder、Claude Desktop 等客户端会对 `readOnlyHint = true` 的工具减少确认弹窗，对 `destructiveHint = true` 的工具给出警告。

选择 C# SDK 的 `[McpServerTool(ReadOnly = true, ...)]`，而不是在客户端手工配置白名单：注解跟着工具定义走，任何客户端接入都自动生效。

**关键坑**：`McpServerToolAttribute` 的属性 getter 默认值是 `Destructive = true`、`OpenWorld = true`（对齐 MCP 规范默认值），但 SDK 在生成 `tools/list` 时**只输出显式设置的 NamedArgument**，未设置就是 `null`。因此不能通过反射读 getter 判断客户端实际收到的值，必须读 `McpServerTool.Create(...).ProtocolTool.Annotations`。测试就是按后者断言的。

### 2.2 ServerInstructions

把"先读 head → 再按需取正文片段 → 用 patch 而不是整篇覆盖 → 冲突要重读"固化成一句服务端指令，比在每个工具的 Description 里重复更省 token（只下发一次），也比依赖系统提示词更可靠。

### 2.3 CallTool 遥测

选择 `McpServerOptions.Filters.Request.CallToolFilters` 而不是 HTTP 中间件：MCP 的工具调用是 JSON-RPC 消息，HTTP 中间件看不到 tool 名称和返回体大小。Filter 能拿到 `CallToolRequestParams` 和 `CallToolResult`，也能拿到 `ClaimsPrincipal`（`kid`）。

不记录参数内容——`create_document` / `apply_document_patch` 的参数就是文档正文，落日志会放大存储和泄露面。

## 3. 实现细节

- `Mcp/*McpTools.cs`：38 个 `[McpServerTool]` 全部补齐注解。
  - 只读（19）：`ReadOnly = true, Destructive = false, OpenWorld = false`
  - `ask_knowledge_vault`：`ReadOnly = true, Destructive = false, OpenWorld = true`（会调用外部 LLM）
  - 幂等写（7）：`Idempotent = true, OpenWorld = false`
  - 破坏性（2）：`delete_revision_comment`、`reindex_knowledge_vault` → `Destructive = true, Idempotent = true`
  - 其余普通写（9）：`OpenWorld = false`
- `Mcp/McpServerInstructions.cs`：指令文本常量，控制在 800 字符以内。
- `Mcp/McpToolTelemetry.cs`：`IConfigureOptions<McpServerOptions>`，向 `CallToolFilters` 注册一个日志过滤器；异常路径记录 Warning 后原样抛出，不改变工具语义。
- `Program.cs`：注册 `ServerInstructions` 和遥测配置器；工具档的 `IPostConfigureOptions` 仍在其后执行，互不影响。
- 测试：`McpToolAnnotationTests`（读/写注解一致性、破坏性/幂等集合、指令长度）、`McpToolTelemetryTests`（过滤器注册）。

## 4. 验证

- `dotnet build KnowledgeVault/KnowledgeVault.csproj`：0 error。
- `dotnet test KnowledgeVault.Api.Tests`：25/25 通过。
- 注解验证方式与客户端一致：对每个方法执行 `McpServerTool.Create(...)`，断言 `ProtocolTool.Annotations` 的 `*Hint` 值。

## 5. 未采纳 / 后续建议

| 方案 | 结论 | 原因 |
|---|---|---|
| `UseStructuredContent` / `OutputSchema` | 暂不启用 | 会同时下发 text + structured，token 反而变多；当前紧凑 JSON 已够用 |
| `/mcp` 限流 | 待定 | 需要按 `kid` 分区并确定配额；属于运维策略，先观察遥测数据里的调用频率 |
| 新增 `readonly` 工具档 | 待定 | 工具档是服务端全局配置；只有单独跑只读实例时才有收益 |
| `list_folder_contents` 分页 | 建议后续 | 当前一次返回整棵子树，超大工作区会放大返回体 |
| 根目录发现 | 建议后续 | 没有 `list_folders`，Agent 只能从文档的 `FolderPath` 反推 folder id |
