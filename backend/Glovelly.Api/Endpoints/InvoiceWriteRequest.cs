using System.Text.Json.Serialization;
using Glovelly.Api.Models;

namespace Glovelly.Api.Endpoints;

// Deliberately not the persisted entity: lifecycle, owner, publication, and
// commercial identity are server-owned. Reject rather than ignore extra fields.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class InvoiceWriteRequest
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid ClientId { get; set; }
    public DateOnly InvoiceDate { get; set; }
    public DateOnly DueDate { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public string? Description { get; set; }
}
