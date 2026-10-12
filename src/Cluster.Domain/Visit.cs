namespace Cluster.Domain;

/// <summary>
/// A single lobby visitor log entry. This is the payload the cluster-nano
/// firmware forwards to <c>POST /api/visits</c> once it has parsed the kiosk
/// form; every value originates from an untrusted visitor and is therefore
/// stored as-is, never interpreted.
/// </summary>
public class Visit
{
    /// <summary>Default used by the firmware when the form omits a visitor type.</summary>
    public const string UnknownVisitorType = "unknown";

    public Guid Id { get; set; } = Guid.NewGuid();

    public string VisitorName { get; set; } = string.Empty;

    public string VisitorType { get; set; } = UnknownVisitorType;

    public string Unit { get; set; } = string.Empty;

    public string TenantId { get; set; } = string.Empty;

    public bool Answered { get; set; }

    public string Notes { get; set; } = string.Empty;

    /// <summary>When the .NET service accepted the visit.</summary>
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
}
