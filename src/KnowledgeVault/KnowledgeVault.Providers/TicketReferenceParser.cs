using System.Text.RegularExpressions;
using KnowledgeVault.Contracts.Documents;
using KnowledgeVault.Infrastructure.Exceptions;

namespace KnowledgeVault.Providers;

public sealed partial class TicketReferenceParser : ITicketReferenceParser
{
    [GeneratedRegex(@"^[A-Z][A-Z0-9]+-[0-9]+$")]
    private static partial Regex TicketKeyRegex();

    public TicketReference? Parse(string? ticketUrl)
    {
        if (string.IsNullOrWhiteSpace(ticketUrl))
        {
            return null;
        }

        var trimmed = ticketUrl.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ValidationException("Ticket URL must be an absolute https URL.");
        }

        var segment = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (string.IsNullOrWhiteSpace(segment) || !TicketKeyRegex().IsMatch(segment))
        {
            throw new ValidationException("Ticket URL must point to a Jira issue key such as RW-71677.");
        }

        return new TicketReference(segment, trimmed);
    }
}
