using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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

    public DbSet<CertificationBodyEntity> CertificationBodies => Set<CertificationBodyEntity>();

    public DbSet<CountryEntity> Countries => Set<CountryEntity>();

    public DbSet<DocumentSequenceEntity> DocumentSequences => Set<DocumentSequenceEntity>();

    public DbSet<GeneralDataEntity> GeneralData => Set<GeneralDataEntity>();

    public DbSet<NotificationEntity> Notifications => Set<NotificationEntity>();

    public DbSet<RoleEntity> Roles => Set<RoleEntity>();

    public DbSet<RolePermissionEntity> RolePermissions => Set<RolePermissionEntity>();

    public DbSet<SchemeEntity> Schemes => Set<SchemeEntity>();

    public DbSet<ServiceProviderEntity> ServiceProviders => Set<ServiceProviderEntity>();

    public DbSet<SupportingDocumentEntity> SupportingDocuments => Set<SupportingDocumentEntity>();

    public DbSet<StateEntity> States => Set<StateEntity>();

    public DbSet<UserEntity> Users => Set<UserEntity>();

    public DbSet<UserCompanyEntity> UserCompanies => Set<UserCompanyEntity>();

    public DbSet<UserTokenEntity> UserTokens => Set<UserTokenEntity>();

    public DbSet<WebLinkEntity> WebLinks => Set<WebLinkEntity>();

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
        modelBuilder.Entity<CertificationBodyEntity>(entity =>
        {
            entity.ToTable("AdmCertificationBodies");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Acronym).HasMaxLength(50);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.Address1).HasMaxLength(200);
            entity.Property(x => x.Address2).HasMaxLength(200);
            entity.Property(x => x.City).HasMaxLength(100);
            entity.Property(x => x.Postcode).HasMaxLength(20);
            entity.Property(x => x.State).HasMaxLength(100);
            entity.Property(x => x.Telephone).HasMaxLength(30);
            entity.Property(x => x.Fax).HasMaxLength(30);
            entity.Property(x => x.Webpage).HasMaxLength(200);
            entity.Property(x => x.Email).HasMaxLength(254);
            entity.Property(x => x.ContactPerson).HasMaxLength(200);
            entity.Property(x => x.BankName).HasMaxLength(200);
            entity.Property(x => x.BankAccountNo).HasMaxLength(50);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            // No navigation properties: the dropdown sources come from their own endpoints
            // (GET api/admin/countries), the list joins for the display name only.
            entity.HasOne<CountryEntity>()
                .WithMany()
                .HasForeignKey(x => x.CountryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CountryEntity>()
                .WithMany()
                .HasForeignKey(x => x.RepresentingCountryId)
                .OnDelete(DeleteBehavior.Restrict);
            // File column group (Database.md 3): the row keeps metadata, the bytes live in
            // IFileStorage (F-06).
            entity.OwnsOne(x => x.Logo, logo =>
            {
                logo.Property(f => f.FileName).IsRequired().HasMaxLength(260);
                logo.Property(f => f.StorageKey).IsRequired().HasMaxLength(100);
                logo.Property(f => f.ContentType).IsRequired().HasMaxLength(100);
            });
        });

        modelBuilder.Entity<CountryEntity>(entity =>
        {
            entity.ToTable("AdmCountries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(100);
            entity.Property(x => x.IsoCode).IsRequired().HasMaxLength(3);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            entity.HasData(ReferenceSeedData.CountryEntities());
        });

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

        modelBuilder.Entity<GeneralDataEntity>(entity =>
        {
            entity.ToTable("AdmGeneralData");
            entity.HasKey(x => x.Id);
            // Enum stored as its spec string (CodingRules 11), e.g. "COMPANY".
            entity.Property(x => x.Group).IsRequired().HasMaxLength(30).HasConversion<string>();
            entity.Property(x => x.Category).IsRequired().HasMaxLength(100);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            // No FK: ComCompanies arrives with C-01. Indexed because the tenant filter runs on
            // every reference-data list.
            entity.HasIndex(x => x.CompanyId);
            // UQ (CompanyId, Group, Category, Name) among live rows (Database.md 3): a soft
            // delete frees the name. The handler returns the friendly message instead of
            // letting this fire.
            entity.HasIndex(x => new { x.CompanyId, x.Group, x.Category, x.Name })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
        });

        modelBuilder.Entity<NotificationEntity>(entity =>
        {
            entity.ToTable("AdmNotifications");
            entity.HasKey(x => x.Id);
            // A-02 added AdmUsers, so the FK deferred in F-08 is wired now. The index keeps
            // the bell lookup ("my unread rows") fast.
            entity.Property(x => x.UserId).IsRequired();
            entity.HasIndex(x => x.UserId);
            entity.HasOne<UserEntity>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.CompanyId);
            entity.Property(x => x.Subject).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Message).IsRequired().HasMaxLength(1000);
            entity.Property(x => x.IsRead).IsRequired();
            entity.Property(x => x.LinkUrl).HasMaxLength(300);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
        });

        modelBuilder.Entity<RoleEntity>(entity =>
        {
            entity.ToTable("AdmRoles");
            entity.HasKey(x => x.Id);
            // No FK: ComCompanies arrives with C-01. Null CompanyId = system role, and the
            // tenant filter never applies to this table (Database.md 5), so the column is
            // deliberately not part of an ITenantEntity.
            entity.Property(x => x.Name).IsRequired().HasMaxLength(100);
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.Property(x => x.IsSystemRole).IsRequired();
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            entity.HasData(RoleSeedData.RoleEntities());
        });

        modelBuilder.Entity<RolePermissionEntity>(entity =>
        {
            entity.ToTable("AdmRolePermissions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PermissionKey).IsRequired().HasMaxLength(100);
            entity.Property(x => x.CanView).IsRequired();
            entity.Property(x => x.CanCreate).IsRequired();
            entity.Property(x => x.CanEdit).IsRequired();
            entity.Property(x => x.CanDelete).IsRequired();
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            entity.HasOne<RoleEntity>()
                .WithMany()
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
            // UQ (RoleId, PermissionKey) among live rows only: revoking a screen is a soft
            // delete, so the same screen can be granted again without colliding (CodingRules 7.2).
            entity.HasIndex(x => new { x.RoleId, x.PermissionKey })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasData(RoleSeedData.PermissionEntities());
        });

        modelBuilder.Entity<SchemeEntity>(entity =>
        {
            entity.ToTable("AdmSchemes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(10);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(100);
            entity.Property(x => x.IsFoodPremise).IsRequired();
            entity.Property(x => x.SortOrder).IsRequired();
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            entity.HasData(ReferenceSeedData.SchemeEntities());
        });

        modelBuilder.Entity<ServiceProviderEntity>(entity =>
        {
            entity.ToTable("AdmServiceProviders");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.Address).IsRequired().HasMaxLength(500);
            entity.Property(x => x.Postcode).IsRequired().HasMaxLength(20);
            entity.Property(x => x.State).IsRequired().HasMaxLength(100);
            entity.Property(x => x.Telephone).IsRequired().HasMaxLength(30);
            entity.Property(x => x.Fax).HasMaxLength(30);
            entity.Property(x => x.Webpage).HasMaxLength(200);
            entity.Property(x => x.Email).IsRequired().HasMaxLength(254);
            entity.Property(x => x.ContactPerson).IsRequired().HasMaxLength(200);
            entity.Property(x => x.BankName).HasMaxLength(200);
            entity.Property(x => x.BankAccountNo).HasMaxLength(50);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            // Tenant table (Database.md 3): CompanyId comes from the JWT, indexed because the
            // global query filter runs on every reference-data list. No unique index - Database.md
            // 3 defines none for this table, so the duplicate-name rule is a handler check (spec
            // 21.9) like the CB screen.
            entity.HasIndex(x => x.CompanyId);
            entity.HasOne<CountryEntity>()
                .WithMany()
                .HasForeignKey(x => x.CountryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.CountryId);
        });

        modelBuilder.Entity<StateEntity>(entity =>
        {
            entity.ToTable("AdmStates");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            entity.HasOne<CountryEntity>()
                .WithMany()
                .HasForeignKey(x => x.CountryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.CountryId);
            entity.HasData(ReferenceSeedData.StateEntities());
        });

        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.ToTable("AdmUsers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Email).IsRequired().HasMaxLength(254);
            entity.Property(x => x.PasswordHash).IsRequired().HasMaxLength(500);
            entity.Property(x => x.MustChangePassword).IsRequired();
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.IsPlatformAdmin).IsRequired();
            entity.Property(x => x.ContactNo).HasMaxLength(30);
            entity.Property(x => x.FailedLoginCount).IsRequired();
            // File column group (Database.md 1) as an owned type; the row has no bytes.
            entity.OwnsOne(x => x.ProfilePicture, picture =>
            {
                picture.Property(f => f.FileName).IsRequired().HasMaxLength(260);
                picture.Property(f => f.StorageKey).IsRequired().HasMaxLength(100);
                picture.Property(f => f.ContentType).IsRequired().HasMaxLength(100);
            });
            entity.HasOne<RoleEntity>()
                .WithMany()
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
            // UQ Email among live rows only (spec 21.9): a soft-deleted user frees the address.
            entity.HasIndex(x => x.Email)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
        });

        modelBuilder.Entity<UserCompanyEntity>(entity =>
        {
            entity.ToTable("AdmUserCompanies");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.IsDefault).IsRequired();
            entity.HasOne<UserEntity>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            // No FK: ComCompanies arrives with C-01. Indexed because login resolves the
            // company list of one user on every sign-in.
            entity.HasIndex(x => x.CompanyId);
            entity.HasIndex(x => new { x.UserId, x.CompanyId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
        });

        modelBuilder.Entity<UserTokenEntity>(entity =>
        {
            entity.ToTable("AdmUserTokens");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Purpose).IsRequired().HasConversion<string>().HasMaxLength(50);
            entity.Property(x => x.TokenHash).IsRequired().HasMaxLength(200);
            entity.Property(x => x.ExpiresAt).IsRequired();
            entity.HasOne<UserEntity>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            // Activation / reset look the token up by its hash alone.
            entity.HasIndex(x => x.TokenHash);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
        });

        modelBuilder.Entity<WebLinkEntity>(entity =>
        {
            entity.ToTable("AdmWebLinks");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Webpage).IsRequired().HasMaxLength(500);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            // Tenant table (Database.md 3): CompanyId from the JWT, indexed because the global
            // query filter runs on every reference-data list. No unique index - Database.md 3
            // defines none for this table, so the duplicate-name rule (spec 21.9) is a handler
            // check, like Service Provider and CB.
            entity.HasIndex(x => x.CompanyId);
            // Icon* is required on the spec 5.4 form, so the owned type is non-null (unlike the
            // optional Logo / ProfilePicture columns) and every column is NOT NULL.
            entity.OwnsOne(x => x.Icon, icon =>
            {
                icon.Property(f => f.FileName).IsRequired().HasMaxLength(260);
                icon.Property(f => f.StorageKey).IsRequired().HasMaxLength(100);
                icon.Property(f => f.ContentType).IsRequired().HasMaxLength(100);
            });
        });

        modelBuilder.Entity<SupportingDocumentEntity>(entity =>
        {
            entity.ToTable("AdmSupportingDocuments");
            entity.HasKey(x => x.Id);
            // Enum stored as its Database.md string (CodingRules 11), e.g. "SopHas".
            entity.Property(x => x.ForView).IsRequired().HasMaxLength(30).HasConversion<string>();
            entity.Property(x => x.DocumentType).IsRequired().HasMaxLength(200);
            entity.Property(x => x.DocumentSequence).IsRequired();
            entity.Property(x => x.IsMandatory).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            // No FK: AdmCompanies arrives with C-01. Indexed because the tenant filter runs on
            // every reference-data list.
            entity.HasIndex(x => x.CompanyId);
            // UQ (CompanyId, ForView, DocumentSequence) among live rows (Database.md 3): a soft
            // delete frees the sequence. The handler returns the manual's message instead of
            // letting this fire (D-16).
            entity.HasIndex(x => new { x.CompanyId, x.ForView, x.DocumentSequence })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            // Template File? (Database.md 3): optional owned type - only the SOP views of
            // spec 5.5 carry one, the other rows keep NULL columns.
            entity.OwnsOne(x => x.Template, template =>
            {
                template.Property(f => f.FileName).HasMaxLength(260);
                template.Property(f => f.StorageKey).HasMaxLength(100);
                template.Property(f => f.ContentType).HasMaxLength(100);
            });
        });
    }

    private void ApplyAuditAndSoftDelete()
    {
        // Removing an owner cascade-deletes its owned dependents (the File column group) as
        // Deleted; run the fixup now so the soft-delete rewrite below sees both sides.
        ChangeTracker.DetectChanges();

        var now = DateTime.UtcNow;
        var user = string.IsNullOrWhiteSpace(_currentUser.UserId) ? SystemUser : _currentUser.UserId;
        var softDeletedOwners = new List<EntityEntry>();

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
                    softDeletedOwners.Add(entry);
                    break;
            }
        }

        // The row survives, so its file metadata must survive with it - cascade delete would
        // drop the columns while the bytes stay in storage, leaving them unreachable. Scoped to
        // the owners just rewritten: an owned instance that is being REPLACED is also tracked as
        // Deleted (old and new share one key) and must stay deleted.
        foreach (var owner in softDeletedOwners)
        {
            foreach (var reference in owner.References)
            {
                if (reference.CurrentValue is null)
                    continue;

                var dependent = Entry(reference.CurrentValue);
                if (dependent.State == EntityState.Deleted)
                    dependent.State = EntityState.Modified;
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
