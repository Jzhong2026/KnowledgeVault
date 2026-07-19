using KnowledgeVault.Domain.Enums;

namespace KnowledgeVault.Providers;

public static class DocumentTemplates
{
    public static string? GetDefaultContent(DocumentType documentType)
    {
        return documentType switch
        {
            DocumentType.PlanningReview => PlanningReview,
            DocumentType.TaskBreakdown => TaskBreakdown,
            DocumentType.ProjectMemory => ProjectMemory,
            _ => null
        };
    }

    public const string ProjectMemoryTitle = "MEMORY.md";

    public const string ProjectMemorySummary =
        "Shared durable context for project members and their agents.";

    public const string ProjectMemory = """
        # MEMORY.md

        > Shared durable context for this project's members and agents. Keep this document concise, current, and free of secrets.

        ## Project Purpose

        <!-- What this project exists to accomplish. -->

        ## Current Context

        <!-- Verified facts an agent needs before starting work. -->

        ## Constraints and Conventions

        <!-- Technical, product, security, and collaboration rules that must be preserved. -->

        ## Key Decisions

        | Date | Decision | Rationale | Reference |
        | --- | --- | --- | --- |
        |  |  |  |  |

        ## Important Locations and Commands

        <!-- Repositories, paths, environments, build/test commands, dashboards, and other stable pointers. -->

        ## Agent Prompts

        <!-- Reusable prompts and instructions proposed by project members and accepted by an administrator. -->

        ## Agent Handoff

        - In progress:
        - Next:
        - Blockers:
        - Last verified:

        ## Open Questions

        <!-- Unresolved questions that can materially change the work. -->

        ## Maintenance Rules

        - Record verified project facts, not chat transcripts or speculation.
        - Update stale entries instead of appending contradictory guidance.
        - Summarize durable decisions here and link to detailed decision records when needed.
        - Never store passwords, tokens, private keys, or other secrets.
        """;

    public const string PlanningReview = """
        # Planning Review

        ## Goal

        ## Scope

        ## Non-Goals

        ## Current State

        ## Approach

        ## Data Model

        ## Permissions

        ## Risks

        ## Acceptance Criteria

        ## Open Questions
        """;

    public const string TaskBreakdown = """
        # Task Breakdown

        ## Dependencies

        ## Tasks

        | Task ID | Objective | Change Location | Acceptance Criteria | Test | Status |
        | --- | --- | --- | --- | --- | --- |
        |  |  |  |  |  |  |
        """;
}
