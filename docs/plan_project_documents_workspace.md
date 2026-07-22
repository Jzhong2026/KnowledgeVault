# 项目文档文件夹（Workspace）功能实现计划

## 1. 目标与背景

当前 KnowledgeVault 的文档（KnowledgeItem）只能通过「Project + Topic（扁平、无层级）」或「Category/Tag」来组织，列表以行式（`knowledge-list`）展示。用户希望把「项目文档」改造成类似 VS Code 的**文件夹工作区（Workspace）**体验：

- 文档下方支持 **Folder**，并以 **Tile（磁贴）** 方式展示；
- 支持 **嵌套 Folder**（多级目录），Folder 与 Document 都按 Tile 展示；
- 打开某个 Folder 后，**隐藏左侧主导航菜单**，在左侧展示一个**新的目录树面板**，显示该 Folder 内部的目录结构；
- 选定一个「工作 Folder」即相当于 VS Code 的 workspace（可记住当前工作区）；
- 提供**明显、可发现的 Open / Exit Workspace** 操作入口。

> 导航范式声明：文档导航以 **Folder 为准**。`ProjectTopic`/`Group` 不再作为文档组织容器，仅保留用于 Project Memory 分组与历史兼容数据，新功能一律以 `FolderId` 作为文档主组织维度。

本计划覆盖后端数据模型、API、以及前端的 Workspace 模式、目录树、Tile 网格与路由改造。

---

## 2. 现状盘点（关键事实）

前端（Angular 21，`src/knowledge-vault-web`）：
- 路由 `project-documents`（scope=Project）与 `knowledge`（scope=Personal）都指向 `KnowledgePage`（`app.routes.ts:32-47`）。
- 文档以 `knowledge-list` 行式组件展示（`features/knowledge/components/knowledge-list`）。
- 全局外壳 `AppShell`（`layout/app-shell`）固定渲染 `Sidebar` + `Topbar` + `RouterOutlet`，无「隐藏侧栏」能力。
- `Sidebar` 仅有 `collapsed` 折叠态，无「进入工作区」概念（`layout/sidebar/sidebar.ts`）。
- `Topbar` 仅有用户菜单，无面包屑/工作区状态（`layout/topbar/topbar.ts`）。
- `ApiClient` 已封装 documents / projects / groups(topics) 接口（`core/api/api-client.service.ts`），但没有 folder 相关接口。

后端（.NET，`src/KnowledgeVault`）：
- 实体 `KnowledgeItem`（`KnowledgeVault.Domain/Entities/KnowledgeItem.cs`）仅有 `ProjectId` 与 `TopicId`，**无 Folder 概念**。
- `ProjectTopic`（`Entities/ProjectTopic.cs`）是扁平的（有 `SortOrder`、无 `ParentId`），用于项目分组/记忆，不是文档目录。
- DTO：`KnowledgeVault.Contracts/Documents/DocumentDtos.cs`、`DocumentRequests.cs`。
- 控制器在 `KnowledgeVault/Controllers/`（含 Documents 相关）。
- 数据访问：`KnowledgeVault.DataAccess`（EF Core，当前迁移为 SQLite，见 git status 中 `InitialSqliteSchema`）。

结论：需在后端新增 **Folder 层级实体**，并在 `KnowledgeItem` 上挂 `FolderId`；前端新增 Workspace 模式与目录树、Tile 网格。

---

## 3. 数据模型设计

### 3.1 新增实体 `Folder`（与 `KnowledgeItem` 同级，位于 `KnowledgeVault.Domain/Entities/Folder.cs`）

```csharp
public sealed class Folder : AuditableEntity
{
    public DocumentScope Scope { get; set; } = DocumentScope.Personal;
    public Guid? ProjectId { get; set; }          // 仅 Project scope 时填充
    public Guid? ParentFolderId { get; set; }     // 自引用，支持嵌套；根目录为 null
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty; // 同级唯一性校验用（小写/去空格）
    public string? Description { get; set; }
    public int SortOrder { get; set; }

    public Project? Project { get; set; }
    public Folder? ParentFolder { get; set; }
    public ICollection<Folder> ChildFolders { get; set; } = [];
    public ICollection<KnowledgeItem> KnowledgeItems { get; set; } = [];
}
```

设计要点：
- **唯一性约束**：父目录范围由 `Scope` + `ProjectId` + `ParentFolderId` 确定，但「节点本身」由 `Id` 唯一标识。同一 `Scope` + `ProjectId` + `ParentFolderId` 组合下，需保证 `NormalizedName`（名称统一小写、去首尾空格）唯一——由数据库唯一索引（含 `Scope`、`ProjectId`、`ParentFolderId`、`NormalizedName`）与 `FolderRepository` 校验保证，避免同级重名。
- `Scope` + `ProjectId` 用于在「项目文档」与「我的文档」之间隔离出两套独立树。
- `ParentFolderId` 自引用实现**任意层级嵌套**；根级 Folder 的 `ParentFolderId = null`。
- 为防环，`FolderRepository` 在移动/创建时需做祖先校验（不允许把文件夹移动到自己的子孙下）。

### 3.2 修改 `KnowledgeItem`

在 `KnowledgeItem.cs` 增加：
```csharp
public Guid? FolderId { get; set; }
public Folder? Folder { get; set; }
```
保留 `ProjectId`/`TopicId` 以兼容历史数据；新功能以 `FolderId` 为主组织维度（Topic 可逐步废弃或仅用于项目记忆分组）。

### 3.3 迁移

新增 EF 配置（`KnowledgeVault.DataAccess` 的 `OnModelCreating` 或单独 `IEntityTypeConfiguration<Folder>`），并建立迁移：
- `Add-Migration AddFolderHierarchy` → 生成 `Folder` 表 + `KnowledgeItems.FolderId` 外键（含索引 `IX_Folder_ParentFolderId`、`IX_Folder_ProjectId`）。
- 当前数据库提供方为 SQLite（git status 显示 `InitialSqliteSchema`），按现有迁移流程生成即可。

---

## 4. 后端 API 设计（新增 `FoldersController`，路由前缀建议 `/folders`）

| 方法 | 路由 | 说明 |
|------|------|------|
| GET | `/folders` | 列出某作用域下、指定 `parentFolderId`（可空=根）的子项：`?scope=&projectId=&parentFolderId=&rootFolderId=` 返回 `{ folders: FolderSummaryDto[], documents: KnowledgeItemSummaryDto[] }`，供 Tile 网格一次性渲染。**Workspace 内容加载时始终带 `rootFolderId=workspaceRootFolderId`**；后端校验 `parentFolderId`（含 null=root 自身）必须是该 root 的子孙或 root 本身，否则返回 400（落实「禁止跳出 workspace root」，与 tree 接口一致） |
| GET | `/folders/tree?scope=&projectId=&rootFolderId=` | 返回以 `rootFolderId` 为根的**子树**（仅 folder 节点）。Workspace 左侧目录树只渲染当前 workspace root 下的目录结构；后端**以 rootFolderId 为边界裁剪**，不返回任何超出 root 的祖先节点，并拒绝把 `currentFolderId` 导航到 root 之外的节点（禁止跳出 workspace root） |
| GET | `/folders/{id}` | 获取单个 Folder 详情 |
| POST | `/folders` | 创建：`{ scope, projectId?, parentFolderId?, name, description?, sortOrder? }` |
| PUT | `/folders/{id}` | 改名/移动：`{ name?, description?, parentFolderId?, sortOrder? }`（移动需环校验） |
| DELETE | `/folders/{id}` | 删除；首版**仅允许删除空文件夹**（见 §7 决策项 1）：后端校验 `ChildFolders.Count == 0 && KnowledgeItems.Count == 0`，否则返回 409 Conflict |
| POST | `/folders/{id}/set-workspace` | （可选）让后端记录「用户当前工作区根 Folder」，返回 workspace 状态 |
| PATCH | `/documents/{id}/metadata` | **移动文档**：请求体含 `folderId`（可置 `null` 移回根）、`categoryId`、`status`、`tagIds` 等。后端必须校验：① 目标 folder 存在（`folderId` 为 null 表示移到根）；② 目标 folder 的 `scope`/`projectId` 与文档完全一致；③ 调用者对目标 folder（或 project）拥有写权限（Owner/Admin/Editor）。*注：Document 不是容器，移动到子 folder 不会成环，故不做防环校验——防环仅适用于移动 Folder。* 返回更新后的文档摘要 |

关键 DTO（`KnowledgeVault.Contracts/Documents/FolderDtos.cs`）：
```csharp
public record FolderSummaryDto(Guid Id, string Name, string? Description, int SortOrder,
    Guid? ParentFolderId, Guid? ProjectId, DocumentScope Scope, int ChildFolderCount, int DocumentCount);
public record FolderTreeNodeDto(Guid Id, string Name, Guid? ParentFolderId, int SortOrder,
    IReadOnlyList<FolderTreeNodeDto> Children);
public record FolderContentDto(IReadOnlyList<FolderSummaryDto> Folders,
    IReadOnlyList<KnowledgeItemSummaryDto> Documents);
```

文档查询扩展：在 `DocumentQuery`（`DocumentRequests.cs`）中增加 `Guid? FolderId`，并支持 `folderId=root` 之类语义，使现有 `/documents` 列表可按文件夹过滤（保持与旧 Topic 过滤兼容）。同时扩展 `UpdateDocumentMetadataRequest`，新增 `Guid? FolderId` 字段作为移动文档的承载（含 scope/project 一致性 + 目标 folder 存在性 + 权限校验；Document 不是容器，不做防环，防环仅适用于移动 Folder）。

权限：复用现有 `DocumentScope` + Project 成员角色校验（`ProjectRole`：Owner/Admin/Editor 可写，Viewer 只读）。

---

## 5. 前端架构设计

### 5.1 新增「Workspace 模式」状态（核心）

新增服务 `WorkspaceService`（`core/workspace/workspace.service.ts`），用单一 `WorkspaceState` 聚合，避免 Personal/Project、不同 project 之间串状态：

```typescript
interface WorkspaceState {
  scope: DocumentScope;                  // 'Personal' | 'Project'
  projectId: string | null;             // Project scope 时填充，Personal 时为 null
  workspaceRootFolderId: string | null; // 进入 Workspace 时的根 Folder（相当于 VS Code workspace 根）
  currentFolderId: string | null;       // 当前所在 Folder，必须是 workspaceRootFolderId 或其子孙
}
```

- `state = signal<WorkspaceState | null>(null)`：`null` = 普通模式；非 null = Workspace 模式。
- `isWorkspaceMode = computed(() => state() != null)`；辅助读取 `workspaceRootFolderId()` / `currentFolderId()`。
- 持久化：用 `localStorage`，以 `scope + ':' + (projectId ?? '')` 为 key 记住整套 `WorkspaceState`；刷新后按当前路由的 scope/project 恢复。切换 scope 或 project 时自动按 key 隔离，杜绝 Personal↔Project、不同 project 之间的状态串扰。
- 守卫：任何把 `currentFolderId` 设为「非 workspaceRootFolderId 子孙（含自身）」的尝试都被拒绝（禁止跳出 workspace root）。

`AppShell` 改造（`layout/app-shell/app-shell.ts` + `.html`）：
- 由原来的「永远显示 Sidebar」，改为：
  - 普通模式：显示 `Sidebar`（现有主导航）。
  - Workspace 模式：`WorkspaceMode` 组件替换左侧栏（隐藏 `Sidebar`），渲染目录树 + Exit 按钮。
- 通过 `WorkspaceService.isWorkspaceMode()` 切换。

### 5.2 新增组件清单（均位于 `features/knowledge/...` 或 `layout/workspace`）

1. `FolderTile`（`components/folder-tile`）：单个文件夹磁贴（图标 + 名称 + 子项数量）。
   - **主操作（左键点击）= Open Folder**：直接打开该 folder，进入其目录（设置 `currentFolderId`）并切换到 Workspace 模式（隐藏左侧主导航、显示目录树）。
   - **Open Workspace（显式、可见入口，不只藏在右键菜单）**：磁贴上提供醒目的「Open Workspace / 打开为工作区」按钮（如角标或 hover 工具栏），点击后把该 folder 设为 `workspaceRootFolderId` 进入 Workspace 模式；此入口同样在页面 toolbar 与目录树节点上提供。
   - 右键/更多菜单保留次级操作：重命名、删除、移动到…。
2. `DocumentTile`（`components/document-tile`）：单个文档磁贴（复用现有 `StatusPill`、`KnowledgeItemSummary` 模型）。
3. `TileGrid`（`components/tile-grid`）：统一网格容器，接收 `folders[]` + `documents[]`，按 `SortOrder`/名称排序，响应式列数（CSS grid `auto-fill minmax(180px,1fr)`）。
4. `FolderTree`（`layout/workspace/folder-tree`）：左侧目录树面板，递归渲染 `FolderTreeNodeDto`，当前 `currentFolderId` 高亮，点击切换 `currentFolderId` 并导航。
5. `WorkspaceMode`（`layout/workspace/workspace-mode`）：组合 `FolderTree` + 顶部「Exit Workspace」按钮 + 面包屑；占据原 Sidebar 区域。
6. `Breadcrumb`（`layout/topbar/breadcrumb` 或复用 Topbar）：在 Workspace 模式下显示「工作区根 / 当前路径...」，每级可点击跳回。

### 5.3 页面改造

- 新建/改造 **Project Documents 页面为 Workspace 页面**：`features/projects/project-workspace-page`（或复用 `knowledge-page` 增加 `viewMode`）。职责：
  - 进入时根据 `WorkspaceService.state().currentFolderId` 调用 `GET /folders?parentFolderId=...&rootFolderId=workspaceRootFolderId` 获取子文件夹 + 文档，渲染 `TileGrid`（带 rootFolderId 由后端保证内容不越出 workspace）。
  - **Folder 磁贴左键点击 = Open Folder**，行为按当前模式区分：
    - **普通模式（尚未进入 Workspace）**：把点击的 folder 同时设为 `workspaceRootFolderId` 与 `currentFolderId`（`root = current = clickedFolderId`），并切换进入 Workspace 模式——此时左侧目录树以该 folder 为 root 渲染。
    - **已处于 Workspace 模式**：点击目录树内 folder 只更新 `currentFolderId`（root 不变），目录树同步展开并高亮当前节点。
  - 这样保证首次进入 Workspace 时 `WorkspaceState` 的 `root`/`current` 都被正确填充，且后续在 workspace 内导航不会抬高 root。
  - 页面 **toolbar 提供显式 Open Workspace 入口**（并提供 Exit Workspace），可从当前目录或任意 folder 触发。
  - 提供「新建文件夹」「新建文档」按钮（调用 `POST /folders` / 现有 `POST /documents`，写入 `FolderId`）；文档列表支持「移动到…」调用 `PATCH /documents/{id}/metadata` 修改 `FolderId`。
- **导航范式声明**：文档导航以 **Folder 为准**。`ProjectTopic`/`Group` 不再作为文档组织容器，仅保留用于 Project Memory 分组与历史兼容；新功能一律以 `FolderId` 作为文档主组织维度。

### 5.4 路由

- 现有 `project-documents` 与 `knowledge` 路由指向新的 Workspace 页面（或新增 `project-workspace`、`my-workspace`，旧路径 redirect）。
- 支持深层链接，参数为 `?workspaceRootFolderId=xxx&folderId=yyy`：
  - 两者都传：直接恢复 Workspace 模式，`workspaceRootFolderId=xxx`、`currentFolderId=yyy`（后端校验 yyy 是 xxx 的子孙或自身，否则回退到 root）。
  - 仅传 `folderId`：规则为 `workspaceRootFolderId = currentFolderId = folderId`（即把该 folder 当作 root 打开，等价于一次 Open Folder）。
  - 无参数：普通模式，从根目录（`currentFolderId=null`）开始。
  - 刷新时优先从 `localStorage` 的 `WorkspaceState` 恢复；URL 参数存在时以 URL 为准（覆盖恢复），解决换设备/首次打开无本地状态时也能定位 workspace root。

### 5.5 顶部栏改造（Topbar）

在 `Topbar` 增加：
- Workspace 模式下显示面包屑与 **「Exit Workspace」** 按钮（调用 `WorkspaceService.exitWorkspace()`）。
- 普通模式下在 Project Documents 入口旁显示「打开工作区」提示。

---

## 6. Open / Exit Workspace 交互（明确要求）

**语义收紧**：Open Folder 与 Open Workspace 是两个明确区分、但都导向 Workspace 模式的操作；两者都不是「藏在右键菜单里」的次级动作。

- **Open Folder（主操作）**：左键点击任意 Folder 磁贴，即打开该文件夹——进入其目录（`currentFolderId = 该 folder`），并立即切换到 Workspace 模式（隐藏左侧主导航，显示以该 folder 所在 workspace root 为边界的目录树）。这是用户浏览目录层级的主要方式。
- **Open Workspace（显式入口）**：把「当前所在 folder」或任意指定 folder 设为 **workspace root**（`workspaceRootFolderId`），从而把左侧目录树的边界固定在该 root 之下。该入口必须在以下三处都可见、可触发，不能只放在右键菜单：
  1. Folder 磁贴上的醒目按钮（如角标/hover 工具栏）；
  2. 页面 toolbar 的「Open Workspace」按钮（把当前目录设为 root）；
  3. 左侧目录树节点上的「Open as Workspace」操作。
- **Exit Workspace**：在 `WorkspaceMode` 面板顶部与 `Topbar` 均提供醒目「Exit Workspace / 退出工作区」按钮。点击后：清空 `WorkspaceState`（`state=null`），恢复左侧主导航；路由回到常规文档列表。
- 两种状态切换需有视觉区分（如左侧栏配色/图标变化、顶部工作区标识），确保用户随时知道自己在「普通模式」还是「工作区模式」。

---

## 7. 需确认的设计决策（实现前与用户对齐）

> 以下第 2/3/4 项经评审已明确决议，按决议实现即可；第 1 项仍为待确认选项。

1. **删除 Folder 策略**（已决议，首版从简）：首版采用「**仅允许删除空文件夹**」——文件夹含子文件夹或文档时拒绝删除并提示先清空，风险最小；级联删除（含子项一并删除）作为后续增强，届时需做强确认（输入名称 / 二次模态确认）。后端在 `DELETE /folders/{id}` 中校验 `ChildFolders.Count == 0 && KnowledgeItems.Count == 0`，否则返回 409 Conflict。
2. **与现有 Topic/Group 的关系（已决议）**：文档导航以 **Folder 为准**；`ProjectTopic`/`Group` 不再作为文档组织容器，仅保留用于 Project Memory 分组与历史兼容。
3. **Workspace 作用域（已决议）**：Personal 与 Project 文档均可设定工作区，且以 `scope + projectId` 为 key 隔离状态，互不串扰。
4. **持久化粒度（已决议）**：按 `scope + projectId` 分别为 key 记住整套 `WorkspaceState`（含 root/current），而非全局单一工作区。

---

## 8. 分阶段实施步骤

### 阶段 A：后端数据模型与 API
1. 新增 `Folder` 实体与 `FolderRepository`/`IEntityTypeConfiguration`。
2. `KnowledgeItem` 增加 `FolderId` 导航与索引。
3. 新增迁移并 `Update-Database`。
4. 新增 `FoldersController` + DTO（`FolderDtos.cs`、请求 Record），实现 list/tree/get/create/update/delete。
5. `DocumentQuery` 增加 `FolderId` 过滤；文档创建/更新请求增加 `FolderId`。
6. 补充权限校验与单元测试（Providers.Tests）。

### 阶段 B：前端基础能力
7. 新增 `WorkspaceService`（signal + localStorage）。
8. `ApiClient` 增加 folder 相关方法（`listFolders`、`getFolderTree`、`createFolder`、`updateFolder`、`deleteFolder`）。
9. 新增 models（`core/models/folder.models.ts`：`FolderSummary`、`FolderTreeNode`、`FolderContent`）。

### 阶段 C：前端 UI 组件
10. 实现 `FolderTile`、`DocumentTile`、`TileGrid`。
11. 实现 `FolderTree`、`WorkspaceMode`、`Breadcrumb`。
12. 改造 `AppShell` 支持 Workspace 模式切换（隐藏 Sidebar）。

### 阶段 D：页面与交互整合
13. 改造/新建 Project Documents 与 My Documents 的 Workspace 页面，渲染 TileGrid + 新建文件夹/文档。
14. `Topbar` 增加面包屑与 Exit Workspace 按钮；Folder 磁贴增加显式 **Open Workspace** 入口（左键点击 = Open Folder 进入目录），toolbar 也提供 Open/Exit Workspace。
15. 路由深层链接 `?workspaceRootFolderId=&folderId=` 与刷新恢复（`localStorage` 优先、URL 参数覆盖；仅 `folderId` 时规则为 root=current=folderId）。

### 阶段 E：打磨与验证
16. 响应式（grid 列数、目录树滚动）、空状态、加载态、删除二次确认。
17. 本地 `ng build` + 后端启动联调；手动走查 Open/Exit Workspace、嵌套目录、刷新恢复。

---

## 9. 关键文件改动清单

后端：
- `KnowledgeVault.Domain/Entities/Folder.cs`（新增）
- `KnowledgeVault.Domain/Entities/KnowledgeItem.cs`（加 FolderId）
- `KnowledgeVault.Contracts/Documents/FolderDtos.cs`（新增）
- `KnowledgeVault.Contracts/Documents/DocumentRequests.cs`（加 FolderId）
- `KnowledgeVault.DataAccess/` 配置 + 迁移
- `KnowledgeVault/Controllers/FoldersController.cs`（新增）
- `KnowledgeVault.Providers.Tests/`（测试）

前端：
- `core/api/api-client.service.ts`（folder 接口）
- `core/models/folder.models.ts`（新增）
- `core/workspace/workspace.service.ts`（新增）
- `layout/app-shell/app-shell.ts` + `.html`（Workspace 模式切换）
- `layout/sidebar/sidebar.*`（保持，普通模式用）
- `layout/workspace/*`（WorkspaceMode、FolderTree、Breadcrumb，新增）
- `layout/topbar/topbar.*`（面包屑 + Exit 按钮）
- `features/knowledge/components/folder-tile`、`document-tile`、`tile-grid`（新增）
- `features/projects/project-workspace-page` 或改造 `knowledge-page`（Workspace 页面）
- `app.routes.ts`（路由/深层链接）

---

## 10. 验收标准

- 项目文档下可创建多级嵌套 Folder，Folder 与 Document 均以 Tile 展示。
- 左键点击 Folder = Open Folder，进入子目录并切换到 Workspace 模式，左侧展示以 workspace root 为边界的目录树（主导航隐藏、且无法跳出 root）。
- 从 Folder 磁贴 / 页面 toolbar / 目录树节点均可触发显式 **Open Workspace**；刷新页面后 `WorkspaceState` 按 scope+project 恢复；「退出工作区」可明显恢复普通模式。
- 已有文档可通过「移动到…」（`PATCH /documents/{id}/metadata`）改变 `FolderId`，scope/project 不一致或越权时后端拒绝。
- 面包屑可逐级跳回上级目录/工作区根。
- 新建文件夹、新建文档、重命名、删除（含确认）均可正常持久化并反映到 UI。
