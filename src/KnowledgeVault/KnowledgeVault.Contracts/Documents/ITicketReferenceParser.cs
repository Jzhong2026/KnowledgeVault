namespace KnowledgeVault.Contracts.Documents;

public sealed record TicketReference(string TicketNo, string TicketUrl);

public interface ITicketReferenceParser
{
    /// <summary>
    /// Parses and validates a ticket URL. Returns null when the input is null/empty.
    /// Throws ValidationException when a non-empty value is not a valid https Jira-style ticket URL.
    /// </summary>
    TicketReference? Parse(string? ticketUrl);
}
