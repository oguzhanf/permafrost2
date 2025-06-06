﻿using Microsoft.EntityFrameworkCore;
using Permafrost2.Data.Models;

namespace Permafrost2.Data;

public class PermafrostDbContext : DbContext
{
    public PermafrostDbContext(DbContextOptions<PermafrostDbContext> options) : base(options)
    {
    }

    // Identity and Permission entities
    public DbSet<User> Users { get; set; }
    public DbSet<Group> Groups { get; set; }
    public DbSet<Application> Applications { get; set; }
    public DbSet<Permission> Permissions { get; set; }
    public DbSet<UserGroupMembership> UserGroupMemberships { get; set; }
    public DbSet<ApplicationPermission> ApplicationPermissions { get; set; }

    // Data collection tracking
    public DbSet<DataCollectionRun> DataCollectionRuns { get; set; }
    public DbSet<DataSource> DataSources { get; set; }

    // Audit logging
    public DbSet<AuditLog> AuditLogs { get; set; }

    // Agent management
    public DbSet<Agent> Agents { get; set; }
    public DbSet<AgentDataSubmission> AgentDataSubmissions { get; set; }
    public DbSet<AgentError> AgentErrors { get; set; }
    public DbSet<AgentErrorReport> AgentErrorReports { get; set; }
    public DbSet<AgentCertificate> AgentCertificates { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.DisplayName).HasMaxLength(256);
            entity.HasIndex(e => e.Username).IsUnique();
        });

        // Configure Group entity
        modelBuilder.Entity<Group>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // Configure Application entity
        modelBuilder.Entity<Application>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // Configure Permission entity
        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Description).HasMaxLength(1000);
        });

        // Configure UserGroupMembership many-to-many relationship
        modelBuilder.Entity<UserGroupMembership>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.GroupId });
            entity.HasOne(e => e.User)
                  .WithMany(u => u.GroupMemberships)
                  .HasForeignKey(e => e.UserId);
            entity.HasOne(e => e.Group)
                  .WithMany(g => g.UserMemberships)
                  .HasForeignKey(e => e.GroupId);
        });

        // Configure ApplicationPermission many-to-many relationship
        modelBuilder.Entity<ApplicationPermission>(entity =>
        {
            entity.HasKey(e => new { e.ApplicationId, e.PermissionId, e.PrincipalId, e.PrincipalType });
            entity.HasOne(e => e.Application)
                  .WithMany(a => a.ApplicationPermissions)
                  .HasForeignKey(e => e.ApplicationId);
            entity.HasOne(e => e.Permission)
                  .WithMany(p => p.ApplicationPermissions)
                  .HasForeignKey(e => e.PermissionId);
        });

        // Configure DataCollectionRun entity
        modelBuilder.Entity<DataCollectionRun>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.HasOne(e => e.DataSource)
                  .WithMany(ds => ds.DataCollectionRuns)
                  .HasForeignKey(e => e.DataSourceId);
        });

        // Configure DataSource entity
        modelBuilder.Entity<DataSource>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // Configure AuditLog entity
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(256);
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(256);
            entity.Property(e => e.EntityId).IsRequired().HasMaxLength(256);
            entity.Property(e => e.UserId).HasMaxLength(256);
        });

        // Configure Agent entity
        modelBuilder.Entity<Agent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Version).IsRequired().HasMaxLength(50);
            entity.Property(e => e.MachineName).IsRequired().HasMaxLength(256);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.HasIndex(e => new { e.MachineName, e.Type }).IsUnique();
        });

        // Configure AgentDataSubmission entity
        modelBuilder.Entity<AgentDataSubmission>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DataType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.HasOne<Agent>()
                .WithMany()
                .HasForeignKey(e => e.AgentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure AgentError entity
        modelBuilder.Entity<AgentError>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ErrorId).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Source).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.StackTrace).HasMaxLength(8000);
            entity.Property(e => e.AdditionalData).HasMaxLength(4000);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Resolution).HasMaxLength(1000);
            entity.Property(e => e.ResolvedBy).HasMaxLength(256);
            entity.HasOne(e => e.Agent)
                  .WithMany()
                  .HasForeignKey(e => e.AgentId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.AgentId, e.ErrorId }).IsUnique();
            entity.HasIndex(e => e.OccurredAt);
            entity.HasIndex(e => new { e.Severity, e.Category });
        });

        // Configure AgentErrorReport entity
        modelBuilder.Entity<AgentErrorReport>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.StatusMessage).HasMaxLength(1000);
            entity.Property(e => e.ProcessingDetails).HasMaxLength(4000);
            entity.HasOne(e => e.Agent)
                  .WithMany()
                  .HasForeignKey(e => e.AgentId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.ReportedAt);
        });

        // Configure AgentCertificate entity
        modelBuilder.Entity<AgentCertificate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Thumbprint).IsRequired().HasMaxLength(256);
            entity.Property(e => e.SerialNumber).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Subject).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.Issuer).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Usage).HasMaxLength(100);
            entity.Property(e => e.RevocationReason).HasMaxLength(100);
            entity.Property(e => e.CertificateData).HasMaxLength(4000);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.HasOne(e => e.Agent)
                  .WithMany()
                  .HasForeignKey(e => e.AgentId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.Thumbprint).IsUnique();
            entity.HasIndex(e => new { e.AgentId, e.Status });
            entity.HasIndex(e => e.NotAfter);
        });
    }
}
