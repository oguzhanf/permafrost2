using System.ComponentModel.DataAnnotations;

namespace Permafrost2.Data.Models;

public class AgentDataSubmission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid AgentId { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string DataType { get; set; } = string.Empty; // Users, Groups, Events, etc.
    
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? ProcessedAt { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = string.Empty; // Pending, Processing, Completed, Failed
    
    [MaxLength(1000)]
    public string? StatusMessage { get; set; }
    
    public int RecordCount { get; set; } = 0;
    
    public int ProcessedCount { get; set; } = 0;
    
    public int ErrorCount { get; set; } = 0;
    
    [MaxLength(4000)]
    public string? ErrorDetails { get; set; }
    
    public long DataSizeBytes { get; set; } = 0;
    
    [MaxLength(256)]
    public string? FileName { get; set; }
    
    [MaxLength(256)]
    public string? FileHash { get; set; }
    
    [MaxLength(4000)]
    public string? Metadata { get; set; } // JSON metadata about the submission
    
    public DateTime? RetryAfter { get; set; }
    
    public int RetryCount { get; set; } = 0;
    
    public int MaxRetries { get; set; } = 3;
}
