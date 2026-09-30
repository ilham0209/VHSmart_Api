using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Shared.Infrastructure.Persistence;

public class VHSmartDbContext(
    DbContextOptions<VHSmartDbContext> options,
    ICurrentUser currentUser) : DbContext(options)
{
    private const string SystemUser = "system";

    private readonly ICurrentUser _currentUser = currentUser;

    // Filled from ICurrentUser when the context is created (one instance per request), so the
    // global query filters below always read the values of the current request.
    public Guid CurrentCompanyId { get; } = currentUser.CompanyId;

    public bool CurrentIsPlatformAdminViewAll { get; } =
        currentUser.IsPlatformAdmin || currentUser.ViewAllCompanies;

    public DbSet<DocumentSequenceEntity> DocumentSequences => Set<DocumentSequenceEntity>();

    public DbSet<NotificationEntity> Notifications => Set<NotificationEntity>();

    public override int SaveChanges()
    {
        ApplyAuditAndSoftDelete();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditAndSoftDelete();
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ApplyTableConfiguration(modelBuilder);
        ApplyGlobalFilters(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }

    private static void ApplyTableConfiguration(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DocumentSequenceEntity>(entity =>
        {
            entity.ToTable("AdmDocumentSequences");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Scope).IsRequired().HasMaxLength(150);
            entity.Property(x => x.LastNumber).IsRequired().IsConcurrencyToken();
            entity.HasIndex(x => x.Scope).IsUnique();
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
        });

        modelBuilder.Entity<NotificationEntity>(entity =>
        {
            entity.ToTable("AdmNotifications");
            entity.HasKey(x => x.Id);
            // No FK yet: AdmUsers arrives with A-01/A-02, and EF cannot reference a table the
            // model does not have. The index keeps the bell lookup ("my unread rows") fast.
            entity.Property(x => x.UserId).IsRequired();
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.CompanyId);
            entity.Property(x => x.Subject).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Message).IsRequired().HasMaxLength(1000);
            entity.Property(x => x.IsRead).IsRequired();
            entity.Property(x => x.LinkUrl).HasMaxLength(300);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
        });
    }

    private void ApplyAuditAndSoftDelete()
    {
        var now = DateTime.UtcNow;
        var user = string.IsNullOrWhiteSpace(_currentUser.UserId) ? SystemUser : _currentUser.UserId;

        foreach (var entry in ChangeTracker.Entries<BaseClass>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.SysUserCreated = user;
                    entry.Entity.SysDateCreated = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.SysUserModified = user;
                    entry.Entity.SysDateModified = now;
                    break;
                case EntityState.Deleted:
                    // Deletes are always soft (CodingRules 7.1): keep the row, flip IsDeleted.
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.SysUserModified = user;
                    entry.Entity.SysDateModified = now;
                    break;
            }
        }
    }

    private void ApplyGlobalFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.IsOwned() || !typeof(BaseClass).IsAssignableFrom(entityType.ClrType))
                continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var notDeleted = Expression.Equal(
                Expression.Property(parameter, nameof(BaseClass.IsDeleted)),
                Expression.Constant(false));

            Expression body = notDeleted;

            if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                var companyMatches = Expression.Equal(
                    Expression.Property(parameter, nameof(ITenantEntity.CompanyId)),
                    Expression.Property(Expression.Constant(this), nameof(CurrentCompanyId)));
                var viewAll = Expression.Property(
                    Expression.Constant(this), nameof(CurrentIsPlatformAdminViewAll));

                body = Expression.AndAlso(body, Expression.OrElse(viewAll, companyMatches));
            }

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(Expression.Lambda(body, parameter));
        }
    }
}
