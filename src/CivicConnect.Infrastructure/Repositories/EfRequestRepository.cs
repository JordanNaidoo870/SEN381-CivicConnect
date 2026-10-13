using CivicConnect.Application.Contracts;
using CivicConnect.Domain.Entities;
using CivicConnect.Domain.Events;
using CivicConnect.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CivicConnect.Infrastructure.Repositories;

public sealed class EfRequestRepository(CivicConnectDbContext db, UserManager<ApplicationUser> users) : IRequestRepository
{
    public async Task<IReadOnlyList<CategoryItem>> CategoriesAsync(CancellationToken ct = default) =>
        await db.RequestCategories.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Name)
            .Select(c => new CategoryItem(c.Id, c.Name)).ToListAsync(ct);

    public async Task<PageResult<RequestSummary>> ListAsync(RequestFilters f, string? requesterId = null,
        string? assignedId = null, CancellationToken ct = default)
    {
        var query = db.ServiceRequests.AsNoTracking().Include(r => r.Category).AsQueryable();
        if (requesterId != null) query = query.Where(r => r.RequesterId == requesterId);
        if (assignedId != null) query = query.Where(r => r.AssignedStaffId == assignedId);
        if (f.Status is { } status) query = query.Where(r => r.Status == status);
        if (f.CategoryId is { } categoryId) query = query.Where(r => r.CategoryId == categoryId);
        if (f.FromUtc is { } from) query = query.Where(r => r.CreatedAtUtc >= from);
        if (f.ToUtc is { } to) query = query.Where(r => r.CreatedAtUtc < to.AddDays(1));
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var term = f.Search.Trim().ToLower();
            query = query.Where(r => r.Title.ToLower().Contains(term) || r.Location.ToLower().Contains(term) ||
                r.Description.ToLower().Contains(term));
        }
        var total = await query.CountAsync(ct);
        var page = Math.Max(1, f.Page);
        var pageSize = Math.Clamp(f.PageSize, 1, 50);
        query = f.Sort == "oldest" ? query.OrderBy(x => x.CreatedAtUtc) :
            f.Sort == "status" ? query.OrderBy(x => x.Status).ThenByDescending(x => x.CreatedAtUtc) :
            query.OrderByDescending(x => x.CreatedAtUtc);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => new RequestSummary(
            x.Id, x.Title, x.Location, x.Category!.Name, x.Status, x.RequesterId,
            x.AssignedStaffId, x.CreatedAtUtc, x.UpdatedAtUtc, x.Version)).ToListAsync(ct);
        return new PageResult<RequestSummary>(items, total, page, pageSize);
    }

    public async Task<RequestDetails?> DetailsAsync(int id, CancellationToken ct = default)
    {
        var r = await db.ServiceRequests.AsNoTracking().Include(x => x.Category)
            .Include(x => x.History).Include(x => x.Comments).Include(x => x.Attachments)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return null;
        return new RequestDetails(new RequestSummary(r.Id, r.Title, r.Location, r.Category!.Name, r.Status,
            r.RequesterId, r.AssignedStaffId, r.CreatedAtUtc, r.UpdatedAtUtc, r.Version), r.Description,
            r.History.OrderByDescending(x => x.ChangedAtUtc).Select(x =>
                new RequestHistoryView(x.OldStatus.ToString(), x.NewStatus.ToString(), x.ChangedByUserId, x.ChangedAtUtc, x.Comment)).ToList(),
            r.Comments.OrderBy(x => x.CreatedAtUtc).Select(x =>
                new RequestCommentView(x.Id, x.AuthorUserId, x.IsOfficial, x.Body, x.CreatedAtUtc)).ToList(),
            r.Attachments.OrderBy(x => x.UploadedAtUtc).Select(x =>
                new RequestAttachmentView(x.Id, x.FileName, x.ContentType, x.UploadedAtUtc)).ToList());
    }

    private static AuditLog Audit(string action, int id, string? actor, string? correlation, string? detail = null) =>
        new() { Action = action, EntityId = id.ToString(), ActorUserId = actor,
            CorrelationId = correlation, TimestampUtc = DateTime.UtcNow, Detail = detail };

    public async Task<OperationResult> CreateAsync(RequestInput input, string requesterId, string? correlationId, CancellationToken ct = default)
    {
        if (!await db.RequestCategories.AnyAsync(c => c.Id == input.CategoryId && c.IsActive, ct))
            return OperationResult.Fail("Select an active category.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var now = DateTime.UtcNow;
        var request = new ServiceRequest { Title = input.Title, Description = input.Description,
            Location = input.Location, CategoryId = input.CategoryId, RequesterId = requesterId,
            CreatedAtUtc = now, UpdatedAtUtc = now };
        db.ServiceRequests.Add(request);
        await db.SaveChangesAsync(ct);
        db.AuditLogs.Add(Audit("CREATE", request.Id, requesterId, correlationId));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return OperationResult.Ok(request.Id);
    }

    public async Task<OperationResult> ChangeStatusAsync(int id, RequestStatus next, int expectedVersion,
        string actorId, string? comment, string? correlationId, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var entity = await db.ServiceRequests.SingleOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null) return OperationResult.Fail("Request not found.");
        if (entity.Version != expectedVersion) return OperationResult.Fail("Conflict: request has changed. Refresh and try again.");
        if (!RequestWorkflow.CanTransition(entity.Status, next)) return OperationResult.Fail("Invalid status transition.");
        var old = entity.Status;
        entity.Status = next;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        entity.ResolvedAtUtc = next is RequestStatus.Resolved or RequestStatus.Closed ? DateTime.UtcNow : null;
        entity.Version++;
        db.RequestStatusHistories.Add(new RequestStatusHistory { ServiceRequestId = id, OldStatus = old,
            NewStatus = next, ChangedByUserId = actorId, ChangedAtUtc = DateTime.UtcNow, Comment = comment });
        db.AuditLogs.Add(Audit("STATUS_UPDATE", id, actorId, correlationId, $"{old}->{next}"));
        try
        {
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return OperationResult.Ok(id);
        }
        catch (DbUpdateConcurrencyException) { await tx.RollbackAsync(ct); return OperationResult.Fail("Conflict: request updated by another user. Refresh and retry."); }
    }

    public async Task<OperationResult> AssignAsync(int id, string staffId, int expectedVersion,
        string actorId, string? correlationId, CancellationToken ct = default)
    {
        var staff = await users.FindByIdAsync(staffId);
        if (staff is null || !await users.IsInRoleAsync(staff, Roles.Staff))
            return OperationResult.Fail("Select an existing staff account.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var entity = await db.ServiceRequests.SingleOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null) return OperationResult.Fail("Request not found.");
        if (entity.Version != expectedVersion) return OperationResult.Fail("Conflict: refresh request and try again.");
        if (entity.Status == RequestStatus.Closed) return OperationResult.Fail("Closed requests cannot be reassigned.");
        entity.AssignedStaffId = staffId;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        entity.Version++;
        db.AuditLogs.Add(Audit("ASSIGN", id, actorId, correlationId, $"Assigned to {staffId}"));
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return OperationResult.Ok(id); }
        catch (DbUpdateConcurrencyException) { await tx.RollbackAsync(ct); return OperationResult.Fail("Conflict: refresh request and retry."); }
    }

    public async Task<OperationResult> AddCommentAsync(int id, string actorId, string body,
        bool official, string? correlationId, CancellationToken ct = default)
    {
        if (!await db.ServiceRequests.AnyAsync(r => r.Id == id, ct)) return OperationResult.Fail("Request not found.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.RequestComments.Add(new RequestComment { ServiceRequestId = id, AuthorUserId = actorId,
            Body = body, IsOfficial = official });
        db.AuditLogs.Add(Audit("COMMENT", id, actorId, correlationId, official ? "Official reply" : "Requester comment"));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return OperationResult.Ok(id);
    }

    public async Task<OperationResult> AddAttachmentAsync(int id, string actorId, string name,
        string contentType, string reference, string? correlationId, CancellationToken ct = default)
    {
        if (!await db.ServiceRequests.AnyAsync(r => r.Id == id, ct)) return OperationResult.Fail("Request not found.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.RequestAttachments.Add(new RequestAttachment { ServiceRequestId = id, FileName = name,
            ContentType = contentType, StorageReference = reference, UploadedByUserId = actorId });
        db.AuditLogs.Add(Audit("ATTACHMENT_ADD", id, actorId, correlationId));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return OperationResult.Ok(id);
    }

    public Task<RequestAttachment?> AttachmentAsync(int id, CancellationToken ct = default) =>
        db.RequestAttachments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<DashboardMetrics> MetricsAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var total = await db.ServiceRequests.CountAsync(ct);
        var pending = await db.ServiceRequests.CountAsync(r => r.Status == RequestStatus.Pending, ct);
        var inProgress = await db.ServiceRequests.CountAsync(r => r.Status == RequestStatus.InProgress, ct);
        var resolved = await db.ServiceRequests.CountAsync(r => r.Status == RequestStatus.Resolved, ct);
        var closed = await db.ServiceRequests.CountAsync(r => r.Status == RequestStatus.Closed, ct);
        var threshold = nowUtc.AddDays(-7); // Proposed reporting convention; configurable later.
        var overdue = await db.ServiceRequests.CountAsync(r =>
            (r.Status == RequestStatus.Pending || r.Status == RequestStatus.InProgress) && r.CreatedAtUtc < threshold, ct);
        // Keep the grouping and sorting SQL-translatable, then build DTOs in memory.
        var categoryCounts = await db.ServiceRequests
            .AsNoTracking()
            .GroupBy(x => x.Category!.Name)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(ct);
        var counts = categoryCounts
            .Select(x => new CategoryCount(x.Name, x.Count))
            .ToList();
        return new DashboardMetrics(total, pending, inProgress, resolved, closed, overdue, counts);
    }

    public async Task<IReadOnlyList<StaffOption>> StaffAsync(CancellationToken ct = default)
    {
        var staff = await users.GetUsersInRoleAsync(Roles.Staff);
        return staff.OrderBy(u => u.DisplayName).Select(u => new StaffOption(u.Id, u.DisplayName.Length > 0 ? u.DisplayName : u.Email ?? u.Id)).ToList();
    }

    public async Task RecordReadAsync(int id, string actorId, string? correlationId, CancellationToken ct = default)
    {
        db.AuditLogs.Add(Audit("READ_DETAIL", id, actorId, correlationId));
        await db.SaveChangesAsync(ct);
    }
}
