using System.ComponentModel.DataAnnotations;

namespace Permafrost2.Data.Models;

public class DataSource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [MaxLength(256)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string Type { get; set; } = string.Empty; // ActiveDirectory, AzureAD, Microsoft365
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    [MaxLength(256)]
    public string? ConnectionString { get; set; }
    
    [MaxLength(4000)]
    public string? Configuration { get; set; } // JSON configuration
    
    public bool IsEnabled { get; set; } = true;
    
    public DateTime? LastRunAt { get; set; }
    
    [MaxLength(50)]
    public string? LastRunStatus { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? LastUpdated { get; set; }
    
    // Navigation properties
    public virtual ICollection<DataCollectionRun> DataCollectionRuns { get; set; } = new List<DataCollectionRun>();
}
