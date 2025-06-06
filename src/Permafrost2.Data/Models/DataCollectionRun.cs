using System.ComponentModel.DataAnnotations;

namespace Permafrost2.Data.Models;

public class DataCollectionRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid DataSourceId { get; set; }
    
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? CompletedAt { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Running"; // Running, Completed, Failed, Cancelled
    
    public int? RecordsProcessed { get; set; }
    
    public int? RecordsAdded { get; set; }
    
    public int? RecordsUpdated { get; set; }
    
    public int? RecordsDeleted { get; set; }
    
    public int? ErrorCount { get; set; }
    
    [MaxLength(4000)]
    public string? ErrorMessage { get; set; }
    
    [MaxLength(4000)]
    public string? Notes { get; set; }
    
    public TimeSpan? Duration => CompletedAt?.Subtract(StartedAt);
    
    // Navigation properties
    public virtual DataSource DataSource { get; set; } = null!;
}
