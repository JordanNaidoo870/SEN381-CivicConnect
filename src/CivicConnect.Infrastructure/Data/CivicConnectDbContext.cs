using CivicConnect.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CivicConnect.Infrastructure.Data;

public sealed class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class CivicConnectDbContext(DbContextOptions<CivicConnectDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
    public DbSet<RequestCategory> RequestCategories => Set<RequestCategory>();
    public DbSet<RequestStatusHistory> RequestStatusHistories => Set<RequestStatusHistory>();
    public DbSet<RequestComment> RequestComments => Set<RequestComment>();
    public DbSet<RequestAttachment> RequestAttachments => Set<RequestAttachment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RequestUpvote> RequestUpvotes => Set<RequestUpvote>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<ApplicationUser>().Property(u => u.DisplayName).HasMaxLength(120);
        builder.Entity<RequestCategory>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(80).IsRequired();
            b.HasIndex(x => x.Name).IsUnique();
        });
        builder.Entity<ServiceRequest>(b =>
        {
            b.Property(x => x.Title).HasMaxLength(120).IsRequired();
            b.Property(x => x.Description).HasMaxLength(3000).IsRequired();
            b.Property(x => x.Location).HasMaxLength(180).IsRequired();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(25);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RequesterId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AssignedStaffId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.RequesterId, x.CreatedAtUtc });
            b.HasIndex(x => new { x.Status, x.CreatedAtUtc });
            b.HasIndex(x => new { x.CategoryId, x.CreatedAtUtc });
            b.HasIndex(x => x.AssignedStaffId);
        });
        builder.Entity<RequestStatusHistory>(b =>
        {
            b.Property(x => x.OldStatus).HasConversion<string>().HasMaxLength(25);
            b.Property(x => x.NewStatus).HasConversion<string>().HasMaxLength(25);
            b.Property(x => x.Comment).HasMaxLength(1500);
            b.HasOne<ServiceRequest>().WithMany(r => r.History).HasForeignKey(x => x.ServiceRequestId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.ServiceRequestId, x.ChangedAtUtc });
        });
        builder.Entity<RequestComment>(b =>
        {
            b.Property(x => x.Body).HasMaxLength(1500).IsRequired();
            b.HasOne<ServiceRequest>().WithMany(r => r.Comments).HasForeignKey(x => x.ServiceRequestId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<RequestAttachment>(b =>
        {
            b.Property(x => x.FileName).HasMaxLength(200).IsRequired();
            b.Property(x => x.ContentType).HasMaxLength(90).IsRequired();
            b.Property(x => x.StorageReference).HasMaxLength(100).IsRequired();
            b.HasOne<ServiceRequest>().WithMany(r => r.Attachments).HasForeignKey(x => x.ServiceRequestId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<AuditLog>(b =>
        {
            b.Property(x => x.Action).HasMaxLength(60).IsRequired();
            b.Property(x => x.EntityType).HasMaxLength(80).IsRequired();
            b.Property(x => x.EntityId).HasMaxLength(80).IsRequired();
            b.Property(x => x.CorrelationId).HasMaxLength(100);
            b.Property(x => x.Detail).HasMaxLength(1000);
            b.HasIndex(x => new { x.EntityType, x.EntityId, x.TimestampUtc });
        });
        builder.Entity<RequestUpvote>().HasKey(x => new { x.ServiceRequestId, x.UserId });
    }
}
