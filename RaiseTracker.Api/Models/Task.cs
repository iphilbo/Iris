namespace Iris.Models;

public class InvestorTask
{
    public string Id { get; set; } = string.Empty;
    public string InvestorId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DueDate { get; set; } = string.Empty; // YYYY-MM-DD
    public bool Done { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
