using DeskVault.Application.Interfaces;
using DeskVault.Domain.Workspaces;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeskVault.Infrastructure.Repositories;

public sealed class SqliteWorkspaceRepository
    : IWorkspaceRepository
{
    private readonly IDbContextFactory<DeskVaultDbContext> _dbContextFactory;

    public SqliteWorkspaceRepository(
        IDbContextFactory<DeskVaultDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task AddAsync(
        Workspace workspace,
        CancellationToken cancellationToken = default)
    {
        EnsurePersistentWorkspace(workspace);

        await using var dbContext =
            await _dbContextFactory.CreateDbContextAsync(
                cancellationToken);

        var workspaceEntity = ToEntity(workspace);

        await dbContext.Workspaces.AddAsync(
            workspaceEntity,
            cancellationToken);

        await dbContext.WorkspaceDocumentMemberships.AddRangeAsync(
            workspace.Memberships.Select(
                membership => new WorkspaceDocumentMembershipEntity
                {
                    WorkspaceId = workspace.Id,
                    DocumentId = membership.DocumentId,
                    Order = membership.Order
                }),
            cancellationToken);

        await dbContext.SaveChangesAsync(
            cancellationToken);
    }

    public async Task<Workspace?> GetByIdAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext =
            await _dbContextFactory.CreateDbContextAsync(
                cancellationToken);

        var workspaceEntity = await dbContext.Workspaces
            .AsNoTracking()
            .FirstOrDefaultAsync(
                workspace => workspace.Id == workspaceId,
                cancellationToken);

        if (workspaceEntity is null)
        {
            return null;
        }

        var memberships = await dbContext
            .WorkspaceDocumentMemberships
            .AsNoTracking()
            .Where(
                membership =>
                    membership.WorkspaceId == workspaceId)
            .OrderBy(
                membership => membership.Order)
            .ToListAsync(cancellationToken);

        return ToDomain(
            workspaceEntity,
            memberships);
    }

    public async Task<IReadOnlyList<Workspace>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext =
            await _dbContextFactory.CreateDbContextAsync(
                cancellationToken);

        var workspaceEntities = await dbContext.Workspaces
            .AsNoTracking()
            .OrderBy(workspace => workspace.Id)
            .ToListAsync(cancellationToken);

        var membershipEntities = await dbContext
            .WorkspaceDocumentMemberships
            .AsNoTracking()
            .OrderBy(membership => membership.WorkspaceId)
            .ThenBy(membership => membership.Order)
            .ToListAsync(cancellationToken);

        return workspaceEntities
            .Select(
                workspace =>
                    ToDomain(
                        workspace,
                        membershipEntities
                            .Where(
                                membership =>
                                    membership.WorkspaceId ==
                                    workspace.Id)
                            .ToList()))
            .ToList();
    }

    public async Task UpdateAsync(
        Workspace workspace,
        CancellationToken cancellationToken = default)
    {
        EnsurePersistentWorkspace(workspace);

        await using var dbContext =
            await _dbContextFactory.CreateDbContextAsync(
                cancellationToken);

        var workspaceEntity = await dbContext.Workspaces
            .FirstOrDefaultAsync(
                existing => existing.Id == workspace.Id,
                cancellationToken);

        if (workspaceEntity is null)
        {
            throw new InvalidOperationException(
                $"Workspace '{workspace.Id}' was not found.");
        }

        workspaceEntity.Name = workspace.Name;
        workspaceEntity.Type = (int)workspace.TypeOfWorkspace;
        workspaceEntity.LastActiveDocumentId =
            workspace.LastActiveDocumentId;

        var existingMemberships = await dbContext
            .WorkspaceDocumentMemberships
            .Where(
                membership =>
                    membership.WorkspaceId == workspace.Id)
            .ToListAsync(cancellationToken);

        dbContext.WorkspaceDocumentMemberships.RemoveRange(
            existingMemberships);

        await dbContext.WorkspaceDocumentMemberships.AddRangeAsync(
            workspace.Memberships.Select(
                membership => new WorkspaceDocumentMembershipEntity
                {
                    WorkspaceId = workspace.Id,
                    DocumentId = membership.DocumentId,
                    Order = membership.Order
                }),
            cancellationToken);

        await dbContext.SaveChangesAsync(
            cancellationToken);
    }

    public async Task DeleteAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext =
            await _dbContextFactory.CreateDbContextAsync(
                cancellationToken);

        var workspaceEntity = await dbContext.Workspaces
            .FirstOrDefaultAsync(
                workspace => workspace.Id == workspaceId,
                cancellationToken);

        if (workspaceEntity is null)
        {
            return;
        }

        dbContext.Workspaces.Remove(workspaceEntity);

        await dbContext.SaveChangesAsync(
            cancellationToken);
    }

    public async Task RemoveDocumentFromAllWorkspacesAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext =
            await _dbContextFactory.CreateDbContextAsync(
                cancellationToken);

        await dbContext.WorkspaceDocumentMemberships
            .Where(
                membership =>
                    membership.DocumentId == documentId)
            .ExecuteDeleteAsync(
                cancellationToken);
    }

    private static void EnsurePersistentWorkspace(
        Workspace workspace)
    {
        if (workspace.TypeOfWorkspace != WorkspaceType.Persistent)
        {
            throw new InvalidOperationException(
                "Only persistent workspaces can be persisted.");
        }
    }

    private static WorkspaceEntity ToEntity(
        Workspace workspace)
    {
        return new WorkspaceEntity
        {
            Id = workspace.Id,
            Name = workspace.Name,
            Type = (int)workspace.TypeOfWorkspace,
            LastActiveDocumentId =
                workspace.LastActiveDocumentId
        };
    }

    private static Workspace ToDomain(
        WorkspaceEntity workspace,
        IReadOnlyCollection<WorkspaceDocumentMembershipEntity>
            memberships)
    {
        var domainMemberships = memberships
            .Select(
                membership =>
                    WorkspaceDocumentMembership.Create(
                        membership.DocumentId,
                        membership.Order))
            .ToList();

        return Workspace.Restore(
            workspace.Id,
            workspace.Name,
            (WorkspaceType)workspace.Type,
            domainMemberships,
            workspace.LastActiveDocumentId);
    }
}
