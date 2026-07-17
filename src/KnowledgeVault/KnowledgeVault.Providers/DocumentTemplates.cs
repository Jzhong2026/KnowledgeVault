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
            _ => null
        };
    }

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
