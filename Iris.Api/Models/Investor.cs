namespace Iris.Models;

public class Investor
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? MainContact { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string Category { get; set; } = string.Empty; // "existing" | "known" | "new" | "Strategic" | "Financial"
    public string Stage { get; set; } = string.Empty; // "target" | "contacted" | "NDA" | "due_diligence" | "soft_commit" | "commit" | "closed" | "dead"
    public string Status { get; set; } = string.Empty;
    public string? Owner { get; set; } // User display name who owns this investor
    public decimal? CommitAmount { get; set; }
    public string? Notes { get; set; }
    public List<InvestorTask> Tasks { get; set; } = new();

    // Audit fields
    public string? CreatedBy { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Optimistic concurrency (SQL Server ROWVERSION)
    public byte[]? RowVersion { get; set; }
}

public class InvestorSummary
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Owner { get; set; }
    public decimal? CommitAmount { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
