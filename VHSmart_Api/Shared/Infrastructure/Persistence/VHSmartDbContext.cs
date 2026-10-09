using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using VHSmart_Api.Shared.Domain;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Domain.RawMaterial;
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

    public DbSet<AuditPrefixEntity> AuditPrefixes => Set<AuditPrefixEntity>();

    public DbSet<RecommendationEntity> Recommendations => Set<RecommendationEntity>();

    public DbSet<FindingEntity> Findings => Set<FindingEntity>();

    public DbSet<FindingRecommendationEntity> FindingRecommendations =>
        Set<FindingRecommendationEntity>();

    public DbSet<CertificationBodyEntity> CertificationBodies => Set<CertificationBodyEntity>();

    public DbSet<CompanyBrandEntity> CompanyBrands => Set<CompanyBrandEntity>();

    public DbSet<CompanyContactEntity> CompanyContacts => Set<CompanyContactEntity>();

    public DbSet<CompanySubscriptionEntity> CompanySubscriptions => Set<CompanySubscriptionEntity>();

    public DbSet<CompanyEntity> Companies => Set<CompanyEntity>();

    public DbSet<CountryEntity> Countries => Set<CountryEntity>();

    public DbSet<DocumentSequenceEntity> DocumentSequences => Set<DocumentSequenceEntity>();

    public DbSet<GeneralDataEntity> GeneralData => Set<GeneralDataEntity>();

    public DbSet<HalalPolicyEntity> HalalPolicies => Set<HalalPolicyEntity>();

    public DbSet<ManufacturerSupplierEntity> ManufacturerSuppliers => Set<ManufacturerSupplierEntity>();

    public DbSet<MinutesMeetingEntity> MinutesMeetings => Set<MinutesMeetingEntity>();

    public DbSet<MinutesMeetingAttachmentEntity> MinutesMeetingAttachments => Set<MinutesMeetingAttachmentEntity>();

    public DbSet<NotificationEntity> Notifications => Set<NotificationEntity>();

    public DbSet<PremiseEntity> Premises => Set<PremiseEntity>();

    public DbSet<PremiseContactEntity> PremiseContacts => Set<PremiseContactEntity>();

    public DbSet<PremiseHostelEntity> PremiseHostels => Set<PremiseHostelEntity>();

    public DbSet<PremiseAttachmentEntity> PremiseAttachments => Set<PremiseAttachmentEntity>();

    public DbSet<MenuAccessibleCompanyEntity> MenuAccessibleCompanies =>
        Set<MenuAccessibleCompanyEntity>();

    public DbSet<MenuRawMaterialEntity> MenuRawMaterials => Set<MenuRawMaterialEntity>();

    public DbSet<MenuEntity> Menus => Set<MenuEntity>();

    public DbSet<MenuConceptEntity> MenuConcepts => Set<MenuConceptEntity>();

    public DbSet<MenuConceptMenuEntity> MenuConceptMenus => Set<MenuConceptMenuEntity>();

    public DbSet<ProductImageEntity> ProductImages => Set<ProductImageEntity>();

    public DbSet<ProductIngredientEntity> ProductIngredients => Set<ProductIngredientEntity>();

    public DbSet<ProductEntity> Products => Set<ProductEntity>();

    public DbSet<BatchEntity> Batches => Set<BatchEntity>();

    public DbSet<BatchProductEntity> BatchProducts => Set<BatchProductEntity>();

    public DbSet<BatchPremiseEntity> BatchPremises => Set<BatchPremiseEntity>();

    public DbSet<ApplicationEntity> Applications => Set<ApplicationEntity>();

    public DbSet<ApplicationStatusHistoryEntity> ApplicationStatusHistories =>
        Set<ApplicationStatusHistoryEntity>();

    public DbSet<ApplicationAdditionalInfoItemEntity> ApplicationAdditionalInfoItems =>
        Set<ApplicationAdditionalInfoItemEntity>();

    public DbSet<ApplicationAttachmentEntity> ApplicationAttachments =>
        Set<ApplicationAttachmentEntity>();

    public DbSet<HalalCertificateEntity> HalalCertificates =>
        Set<HalalCertificateEntity>();

    public DbSet<CertificateItemEntity> CertificateItems =>
        Set<CertificateItemEntity>();

    public DbSet<RawMaterialAccessibleCompanyEntity> RawMaterialAccessibleCompanies =>
        Set<RawMaterialAccessibleCompanyEntity>();

    public DbSet<RawMaterialAttachmentEntity> RawMaterialAttachments =>
        Set<RawMaterialAttachmentEntity>();

    public DbSet<RawMaterialEntity> RawMaterials => Set<RawMaterialEntity>();

    public DbSet<RoleEntity> Roles => Set<RoleEntity>();

    public DbSet<RolePermissionEntity> RolePermissions => Set<RolePermissionEntity>();

    public DbSet<SchemeEntity> Schemes => Set<SchemeEntity>();

    public DbSet<ServiceProviderEntity> ServiceProviders => Set<ServiceProviderEntity>();

    public DbSet<StaffAttachmentEntity> StaffAttachments => Set<StaffAttachmentEntity>();

    public DbSet<StaffEntity> Staffs => Set<StaffEntity>();

    public DbSet<SupportingDocumentEntity> SupportingDocuments => Set<SupportingDocumentEntity>();

    public DbSet<StateEntity> States => Set<StateEntity>();

    public DbSet<SubscriptionPackageEntity> SubscriptionPackages => Set<SubscriptionPackageEntity>();

    public DbSet<TrainingEntity> Trainings => Set<TrainingEntity>();

    public DbSet<TrainingAttendeeEntity> TrainingAttendees => Set<TrainingAttendeeEntity>();

    public DbSet<TrainingModuleEntity> TrainingModules => Set<TrainingModuleEntity>();

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
        modelBuilder.Entity<AuditPrefixEntity>(entity =>
        {
            entity.ToTable("AudAuditPrefixes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Prefix).IsRequired().HasMaxLength(20);
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            entity.HasIndex(x => x.CompanyId);
            // The BrandId edge is the AdmGeneralData (Group COMPANY, Category Brand) row the
            // prefix slot belongs to; deletes are always soft (CodingRules 7.1).
            entity.HasOne<GeneralDataEntity>()
                .WithMany()
                .HasForeignKey(x => x.BrandId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            // UQ (CompanyId, BrandId) among live rows (Database.md 14.2 "VERIFY, default
            // enforce"): one prefix slot per brand; a soft delete frees the slot. The handler
            // returns the friendly message instead of letting this fire.
            entity.HasIndex(x => new { x.CompanyId, x.BrandId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
        });

        modelBuilder.Entity<RecommendationEntity>(entity =>
        {
            entity.ToTable("AudRecommendations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(2000);
            entity.Property(x => x.RecommendationCode).IsRequired().HasMaxLength(50);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            entity.HasIndex(x => x.CompanyId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            // No unique rule for recommendations (Database.md 14.3 "Free-text code"): the
            // spec states none and codes look free (spec 14.3 samples).
        });

        modelBuilder.Entity<FindingEntity>(entity =>
        {
            entity.ToTable("AudFindings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(2000);
            entity.Property(x => x.FindingCode).IsRequired().HasMaxLength(100);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            entity.HasIndex(x => x.CompanyId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            // UQ (CompanyId, FindingCode) among live rows (Database.md 11): a soft delete
            // frees the code. The handler returns 409 with a friendly message instead of
            // letting this fire on a race.
            entity.HasIndex(x => new { x.CompanyId, x.FindingCode })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            // Spec 21.9 claims the description is unique too, but Database.md 11 carries no
            // such index and its conventions express unique rules as filtered indexes -
            // description uniqueness is not enforced (flagged in AU-03 report).
        });

        modelBuilder.Entity<FindingRecommendationEntity>(entity =>
        {
            entity.ToTable("AudFindingRecommendations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            entity.HasIndex(x => x.CompanyId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            // No navigation properties: the links are always read through the two id columns
            // (GetAllFindings joins, Create/Update/Delete manage the rows directly).
            entity.HasIndex(x => x.FindingId);
            entity.HasOne<FindingEntity>()
                .WithMany()
                .HasForeignKey(x => x.FindingId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.RecommendationId);
            entity.HasOne<RecommendationEntity>()
                .WithMany()
                .HasForeignKey(x => x.RecommendationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.FindingId, x.RecommendationId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
        });

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

        modelBuilder.Entity<CompanyEntity>(entity =>
        {
            entity.ToTable("ComCompanies");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(200);
            entity.Property(x => x.RegistrationType).HasMaxLength(100);
            entity.Property(x => x.BusinessRegistrationNo).IsRequired().HasMaxLength(50);
            entity.Property(x => x.OwnerStatus).HasMaxLength(100);
            entity.Property(x => x.Address1).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Address2).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Address3).HasMaxLength(200);
            entity.Property(x => x.PostCode).IsRequired().HasMaxLength(20);
            entity.Property(x => x.City).HasMaxLength(100);
            entity.Property(x => x.District).IsRequired().HasMaxLength(100);
            entity.Property(x => x.State).IsRequired().HasMaxLength(100);
            entity.Property(x => x.Telephone).IsRequired().HasMaxLength(30);
            entity.Property(x => x.Fax).HasMaxLength(30);
            entity.Property(x => x.IndustrySize).HasMaxLength(100);
            entity.Property(x => x.WebsiteUrl).HasMaxLength(200);
            entity.Property(x => x.Email).IsRequired().HasMaxLength(254);
            entity.Property(x => x.DateOfEstablishment).HasColumnType("date");
            entity.Property(x => x.MainProductsServices).HasMaxLength(500);
            entity.Property(x => x.Market).HasMaxLength(100);
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            // Tenant root (Database.md 3): the row has no CompanyId, so it is deliberately not
            // an ITenantEntity and only the IsDeleted filter applies (platform admin sees all).
            // UQ BusinessRegistrationNo / Email among live rows (Database.md 3, spec 6.1): a
            // soft delete frees both, the handlers return the friendly message instead of
            // letting these fire.
            entity.HasIndex(x => x.BusinessRegistrationNo)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => x.Email)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasOne(x => x.CertificationBody)
                .WithMany()
                .HasForeignKey(x => x.CertificationBodyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.CertificationBodyId);
            entity.HasOne<CountryEntity>()
                .WithMany()
                .HasForeignKey(x => x.CountryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.CountryId);
            entity.HasOne<SchemeEntity>()
                .WithMany()
                .HasForeignKey(x => x.SchemeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.SchemeId);
        });

        modelBuilder.Entity<CompanyBrandEntity>(entity =>
        {
            entity.ToTable("ComCompanyBrands");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            // UQ (CompanyId, BrandId) among live rows (Database.md 3): unlinking is a soft
            // delete, so the filtered index frees the pair and lets the same brand be re-linked.
            entity.HasIndex(x => new { x.CompanyId, x.BrandId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => x.CompanyId);
            entity.HasIndex(x => x.BrandId);
            entity.HasOne<CompanyEntity>()
                .WithMany(x => x.Brands)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Brand)
                .WithMany()
                .HasForeignKey(x => x.BrandId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CompanyContactEntity>(entity =>
        {
            entity.ToTable("ComCompanyContacts");
            entity.HasKey(x => x.Id);
            // Enum stored as its spec string (CodingRules 11), e.g. "HalalExecutive".
            entity.Property(x => x.Kind).IsRequired().HasMaxLength(30).HasConversion<string>();
            entity.Property(x => x.WorkingHourFrom).HasColumnType("time");
            entity.Property(x => x.WorkingHourTo).HasColumnType("time");
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            // UQ (CompanyId, Kind, StaffId) among live rows (Database.md 4): a soft delete
            // frees the pair so the same staff member can be picked again later. The screen
            // keeps one live row per kind and reconciles in the handler, so this never fires
            // on a save - the index only protects data loaded from elsewhere.
            entity.HasIndex(x => new { x.CompanyId, x.Kind, x.StaffId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => x.CompanyId);
            entity.HasIndex(x => x.StaffId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Staff)
                .WithMany()
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HalalPolicyEntity>(entity =>
        {
            entity.ToTable("ComHalalPolicies");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PolicyDate).HasColumnType("date");
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            // One policy per scheme per company among live rows (Database.md 3, spec 7.2
            // [VERIFY] default "enforce"): deleting the row frees the pair for a re-upload.
            entity.HasIndex(x => new { x.CompanyId, x.SchemeId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => x.CompanyId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Scheme)
                .WithMany()
                .HasForeignKey(x => x.SchemeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.SchemeId);
            // Document* is required (Database.md 3): the NOT NULL File column group; the bytes
            // stay in IFileStorage, the row only carries the metadata.
            entity.OwnsOne(x => x.Document, document =>
            {
                document.Property(f => f.FileName).IsRequired().HasMaxLength(260);
                document.Property(f => f.StorageKey).IsRequired().HasMaxLength(100);
                document.Property(f => f.ContentType).IsRequired().HasMaxLength(100);
            });
        });

        modelBuilder.Entity<MinutesMeetingEntity>(entity =>
        {
            entity.ToTable("ComMinutesMeetings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).IsRequired().HasMaxLength(200);
            entity.Property(x => x.MeetingDate).HasColumnType("date");
            entity.Property(x => x.StartTime).HasColumnType("time");
            entity.Property(x => x.EndTime).HasColumnType("time");
            entity.Property(x => x.Location).IsRequired().HasMaxLength(200);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            entity.HasIndex(x => x.CompanyId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MinutesMeetingAttachmentEntity>(entity =>
        {
            entity.ToTable("ComMinutesMeetingAttachments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            entity.HasIndex(x => x.CompanyId);
            entity.HasIndex(x => x.MinutesMeetingId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.MinutesMeeting)
                .WithMany()
                .HasForeignKey(x => x.MinutesMeetingId)
                .OnDelete(DeleteBehavior.Restrict);
            // Document File is optional (Database.md 4 marks no asterisk): nullable owned
            // columns, same shape as the training module document.
            entity.OwnsOne(x => x.Document, document =>
            {
                document.Property(f => f.FileName).IsRequired().HasMaxLength(260);
                document.Property(f => f.StorageKey).IsRequired().HasMaxLength(100);
                document.Property(f => f.ContentType).IsRequired().HasMaxLength(100);
            });
        });

        modelBuilder.Entity<PremiseEntity>(entity =>
        {
            entity.ToTable("ComPremises");
            entity.HasKey(x => x.Id);
            // Enum stored as its spec string (CodingRules 11), e.g. "CentralKitchen".
            entity.Property(x => x.PremiseType).IsRequired().HasMaxLength(30).HasConversion<string>();
            entity.Property(x => x.Name).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Email).IsRequired().HasMaxLength(254);
            entity.Property(x => x.StoreCode).HasMaxLength(50);
            entity.Property(x => x.PremiseManagerName).HasMaxLength(200);
            entity.Property(x => x.BusinessRegistrationNo).HasMaxLength(50);
            entity.Property(x => x.GoogleMapLink).HasMaxLength(500);
            entity.Property(x => x.Address1).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Address2).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Address3).HasMaxLength(200);
            entity.Property(x => x.Postcode).IsRequired().HasMaxLength(20);
            entity.Property(x => x.City).HasMaxLength(100);
            entity.Property(x => x.District).HasMaxLength(100);
            entity.Property(x => x.State).IsRequired().HasMaxLength(100);
            entity.Property(x => x.Telephone).IsRequired().HasMaxLength(30);
            entity.Property(x => x.Fax).HasMaxLength(30);
            entity.Property(x => x.OpeningDate).HasColumnType("date");
            entity.Property(x => x.ClosingDate).HasColumnType("date");
            // Status is a free string in Database.md 7 (spec 7.7 shows "e.g. ACTIVE" only).
            entity.Property(x => x.Status).IsRequired().HasMaxLength(30);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            // UQ (CompanyId, Email) and (CompanyId, StoreCode) among live rows (Database.md 7,
            // spec 7.7 [CODE]): a soft delete frees both; the handler answers the friendly 409
            // first. StoreCode NULLs never collide (unique indexes ignore NULL).
            entity.HasIndex(x => new { x.CompanyId, x.Email })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => new { x.CompanyId, x.StoreCode })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => x.CompanyId);
            entity.HasIndex(x => x.CountryId);
            entity.HasIndex(x => x.BrandId);
            entity.HasIndex(x => x.PremiseManagerStaffId);
            entity.HasIndex(x => x.AreaManagerStaffId);
            entity.HasIndex(x => x.OperationManagerStaffId);
            entity.HasIndex(x => x.PrayerRoomAvailabilityId);
            entity.HasIndex(x => x.TagId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Country)
                .WithMany()
                .HasForeignKey(x => x.CountryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Brand)
                .WithMany()
                .HasForeignKey(x => x.BrandId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.PremiseManager)
                .WithMany()
                .HasForeignKey(x => x.PremiseManagerStaffId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.AreaManager)
                .WithMany()
                .HasForeignKey(x => x.AreaManagerStaffId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.OperationManager)
                .WithMany()
                .HasForeignKey(x => x.OperationManagerStaffId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.PrayerRoomAvailability)
                .WithMany()
                .HasForeignKey(x => x.PrayerRoomAvailabilityId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Tag)
                .WithMany()
                .HasForeignKey(x => x.TagId)
                .OnDelete(DeleteBehavior.Restrict);
            // Database.md 7: the premise points at a Menu Concept. The column existed from
            // PR-01 but the FK was deferred until PrdMenuConcepts landed (PD-05).
            entity.HasIndex(x => x.MenuConceptId);
            entity.HasOne<MenuConceptEntity>()
                .WithMany()
                .HasForeignKey(x => x.MenuConceptId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PremiseContactEntity>(entity =>
        {
            entity.ToTable("ComPremiseContacts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            // UQ (PremiseId, StaffId) among live rows (Database.md 7): the contact list is
            // replaced on save, so a removed contact frees the pair for a later re-pick.
            entity.HasIndex(x => new { x.PremiseId, x.StaffId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => x.CompanyId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Premise)
                .WithMany()
                .HasForeignKey(x => x.PremiseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Staff)
                .WithMany()
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PremiseHostelEntity>(entity =>
        {
            entity.ToTable("ComPremiseHostels");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.HostelName).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Address).HasMaxLength(500);
            entity.Property(x => x.TenancyExpiryDate).HasColumnType("date");
            entity.Property(x => x.ContactPerson).HasMaxLength(200);
            entity.Property(x => x.PhoneNo).HasMaxLength(30);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            entity.HasIndex(x => x.CompanyId);
            entity.HasIndex(x => x.PremiseId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Premise)
                .WithMany()
                .HasForeignKey(x => x.PremiseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PremiseAttachmentEntity>(entity =>
        {
            entity.ToTable("ComPremiseAttachments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DocumentType).IsRequired().HasMaxLength(100);
            entity.Property(x => x.ExpiryDate).HasColumnType("date");
            entity.Property(x => x.ReferenceNo).HasMaxLength(100);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            // One current row per (PremiseId, DocumentType) among live rows (Database.md 7):
            // the upload replaces the row in place for its type, and a soft delete frees the
            // pair for a later re-upload.
            entity.HasIndex(x => new { x.PremiseId, x.DocumentType })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => x.CompanyId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Premise)
                .WithMany()
                .HasForeignKey(x => x.PremiseId)
                .OnDelete(DeleteBehavior.Restrict);
            // Required File group (Database.md 7): the columns are NOT NULL even though the
            // navigation is always set by the upload path.
            entity.OwnsOne(x => x.Document, document =>
            {
                document.Property(f => f.FileName).IsRequired().HasMaxLength(260);
                document.Property(f => f.StorageKey).IsRequired().HasMaxLength(100);
                document.Property(f => f.ContentType).IsRequired().HasMaxLength(100);
                document.Property(f => f.SizeBytes).IsRequired();
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
            // Indexed because the tenant filter runs on every reference-data list; the ERD edge
            // GeneralData }o--|| Company_Tenant is wired with C-01 (Restrict - deletes are
            // always soft, CodingRules 7.1).
            entity.HasIndex(x => x.CompanyId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            // UQ (CompanyId, Group, Category, Name) among live rows (Database.md 3): a soft
            // delete frees the name. The handler returns the friendly message instead of
            // letting this fire.
            entity.HasIndex(x => new { x.CompanyId, x.Group, x.Category, x.Name })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
        });

        modelBuilder.Entity<ManufacturerSupplierEntity>(entity =>
        {
            entity.ToTable("RawManufacturerSuppliers");
            entity.HasKey(x => x.Id);
            // Enum stored as its spec string (CodingRules 11), e.g. "Both".
            entity.Property(x => x.Type).IsRequired().HasMaxLength(20).HasConversion<string>();
            // Every half column is nullable: which half is filled depends on Type, so the
            // part that does not apply stays null and the list shows "N/A" (spec 10.1).
            entity.Property(x => x.ManufacturerName).HasMaxLength(200);
            entity.Property(x => x.ManufacturerBusinessRegNo).HasMaxLength(50);
            entity.Property(x => x.ManufacturerAddress).HasMaxLength(500);
            entity.Property(x => x.ManufacturerPersonInCharge).HasMaxLength(200);
            entity.Property(x => x.ManufacturerContactNo).HasMaxLength(30);
            entity.Property(x => x.ManufacturerEmail).HasMaxLength(254);
            entity.Property(x => x.ManufacturerWebpage).HasMaxLength(200);
            entity.Property(x => x.SupplierName).HasMaxLength(200);
            entity.Property(x => x.SupplierAddress).HasMaxLength(500);
            entity.Property(x => x.SupplierPersonInCharge).HasMaxLength(200);
            entity.Property(x => x.SupplierContactNo).HasMaxLength(30);
            entity.Property(x => x.SupplierEmail).HasMaxLength(254);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            // Tenant table (Database.md 8): CompanyId from the JWT, indexed because the global
            // query filter runs on every list.
            entity.HasIndex(x => x.CompanyId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CountryEntity>()
                .WithMany()
                .HasForeignKey(x => x.ManufacturerCountryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CountryEntity>()
                .WithMany()
                .HasForeignKey(x => x.SupplierCountryId)
                .OnDelete(DeleteBehavior.Restrict);
            // AdmGeneralData (COMPANY / Manufacturer Type) - wired now that the table exists.
            entity.HasIndex(x => x.ManufacturerTypeId);
            entity.HasOne<GeneralDataEntity>()
                .WithMany()
                .HasForeignKey(x => x.ManufacturerTypeId)
                .OnDelete(DeleteBehavior.Restrict);
            // UQ (CompanyId, ManufacturerEmail) and (CompanyId, SupplierEmail) among live rows
            // and only where the column is set (Database.md 8, spec 21.9): a manufacturer-only
            // row has no supplier e-mail, and a soft delete frees the address for reuse. The
            // handlers return the friendly message instead of letting these fire.
            entity.HasIndex(x => new { x.CompanyId, x.ManufacturerEmail })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [ManufacturerEmail] IS NOT NULL");
            entity.HasIndex(x => new { x.CompanyId, x.SupplierEmail })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [SupplierEmail] IS NOT NULL");
            // Logo File? is optional (Database.md 8): nullable owned columns, exactly like
            // AdmCertificationBodies.Logo; the bytes live in IFileStorage (F-06).
            entity.OwnsOne(x => x.Logo, logo =>
            {
                logo.Property(f => f.FileName).IsRequired().HasMaxLength(260);
                logo.Property(f => f.StorageKey).IsRequired().HasMaxLength(100);
                logo.Property(f => f.ContentType).IsRequired().HasMaxLength(100);
            });
        });

        modelBuilder.Entity<RawMaterialEntity>(entity =>
        {
            entity.ToTable("RawMaterials");
            entity.HasKey(x => x.Id);
            // Enum stored as its spec string (CodingRules 11), e.g. "Supporting".
            entity.Property(x => x.Category).IsRequired().HasMaxLength(20).HasConversion<string>();
            entity.Property(x => x.Ingredient).HasMaxLength(200);
            entity.Property(x => x.IngredientCode).HasMaxLength(50);
            entity.Property(x => x.CommercialName).HasMaxLength(200);
            entity.Property(x => x.ScientificName).HasMaxLength(200);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            // Tenant table (Database.md 8): CompanyId from the JWT, indexed because the global
            // query filter runs on every list.
            entity.HasIndex(x => x.CompanyId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.IngredientStatusId);
            entity.HasOne<GeneralDataEntity>()
                .WithMany()
                .HasForeignKey(x => x.IngredientStatusId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.IngredientSourceId);
            entity.HasOne<GeneralDataEntity>()
                .WithMany()
                .HasForeignKey(x => x.IngredientSourceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.ManufacturerSupplierId);
            entity.HasOne<ManufacturerSupplierEntity>()
                .WithMany()
                .HasForeignKey(x => x.ManufacturerSupplierId)
                .OnDelete(DeleteBehavior.Restrict);
            // D-17: the ingredient code is unique per company among live rows. A blank code is
            // not compared (the spec form does not require one) and a soft delete frees it; the
            // handler returns the friendly message instead of letting this fire.
            entity.HasIndex(x => new { x.CompanyId, x.IngredientCode })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [IngredientCode] IS NOT NULL");
        });

        modelBuilder.Entity<RawMaterialAccessibleCompanyEntity>(entity =>
        {
            entity.ToTable("RawMaterialAccessibleCompanies");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            // No CompanyId here (Database.md 8 is not marked [T]): the row belongs to the raw
            // material, and AccessibleCompanyId is the shared-with company.
            entity.HasIndex(x => x.RawMaterialId);
            entity.HasOne<RawMaterialEntity>()
                .WithMany(x => x.AccessibleCompanies)
                .HasForeignKey(x => x.RawMaterialId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.AccessibleCompanyId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.AccessibleCompanyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RawMaterialAttachmentEntity>(entity =>
        {
            entity.ToTable("RawMaterialAttachments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExpiryDate).HasColumnType("date");
            // Database.md 8 gives the row its own status column: written from the calculator
            // when the file is stored, re-computed on every read (D-04) - a snapshot for
            // reporting, never what a screen shows.
            entity.Property(x => x.DocumentStatus).HasMaxLength(30);
            entity.Property(x => x.ReferenceNo).HasMaxLength(100);
            entity.Property(x => x.Authority).HasMaxLength(200);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            // No unique index: Database.md 8 states no "one row per type" rule (the premise
            // tab states one explicitly and gets an index), so the upload replaces the row it
            // finds for the type and nothing constrains the table itself.
            entity.HasIndex(x => x.CompanyId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.RawMaterialId);
            entity.HasOne(x => x.RawMaterial)
                .WithMany()
                .HasForeignKey(x => x.RawMaterialId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.DocumentTypeId);
            entity.HasOne(x => x.DocumentType)
                .WithMany()
                .HasForeignKey(x => x.DocumentTypeId)
                .OnDelete(DeleteBehavior.Restrict);
            // Required File group (Database.md 8 "_ File"): a row only exists once a file has
            // been uploaded, exactly like the premise attachment.
            entity.OwnsOne(x => x.Document, document =>
            {
                document.Property(f => f.FileName).IsRequired().HasMaxLength(260);
                document.Property(f => f.StorageKey).IsRequired().HasMaxLength(100);
                document.Property(f => f.ContentType).IsRequired().HasMaxLength(100);
                document.Property(f => f.SizeBytes).IsRequired();
            });
        });

        modelBuilder.Entity<MenuEntity>(entity =>
        {
            entity.ToTable("PrdMenus");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.Description).HasMaxLength(1000);
            // "Menu Name*" and "Description" are required by the 9.2 form while Database.md 9
            // keeps both columns nullable - the validators enforce the form, the schema does not.
            // Status / Start / End are the documented lengths and types (spec 9.2 "VALIDITY DATE").
            entity.Property(x => x.Status).IsRequired().HasMaxLength(20);
            entity.Property(x => x.StartDate).HasColumnType("date");
            entity.Property(x => x.EndDate).HasColumnType("date");
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            // Tenant table (Database.md 9): CompanyId from the JWT, indexed because the global
            // query filter runs on every list.
            entity.HasIndex(x => x.CompanyId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.CategoryId);
            entity.HasOne<GeneralDataEntity>()
                .WithMany()
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            // Database.md 9: UQ (CompanyId, CategoryId, Name) among live rows - spec 9.2 "menu
            // name must be unique per company within its reference group/category". Soft delete
            // frees the name and the handler returns the friendly message instead of this firing.
            entity.HasIndex(x => new { x.CompanyId, x.CategoryId, x.Name })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
        });

        modelBuilder.Entity<MenuAccessibleCompanyEntity>(entity =>
        {
            entity.ToTable("PrdMenuAccessibleCompanies");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            // No CompanyId here (Database.md 9 does not mark the table [T]): the row belongs to
            // the menu, and AccessibleCompanyId is the shared-with company - the Raw Material
            // shape, so the visibility filter can read it the same way.
            entity.HasIndex(x => x.MenuId);
            entity.HasOne<MenuEntity>()
                .WithMany(x => x.AccessibleCompanies)
                .HasForeignKey(x => x.MenuId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.AccessibleCompanyId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.AccessibleCompanyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MenuRawMaterialEntity>(entity =>
        {
            entity.ToTable("PrdMenuRawMaterials");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            entity.HasIndex(x => x.CompanyId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.MenuId);
            entity.HasOne<MenuEntity>()
                .WithMany(x => x.RawMaterials)
                .HasForeignKey(x => x.MenuId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.RawMaterialId);
            entity.HasOne<RawMaterialEntity>()
                .WithMany()
                .HasForeignKey(x => x.RawMaterialId)
                .OnDelete(DeleteBehavior.Restrict);
            // UQ (MenuId, RawMaterialId) (Database.md 9): saving the modal replaces the list, so
            // a dropped pair is a soft delete (CodingRules 7.1) and only the LIVE rows are kept
            // unique - the same filter PD-02 gave PrdProductIngredients. The handlers answer the
            // friendly message instead of letting this fire.
            entity.HasIndex(x => new { x.MenuId, x.RawMaterialId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
        });

        modelBuilder.Entity<MenuConceptEntity>(entity =>
        {
            entity.ToTable("PrdMenuConcepts");
            entity.HasKey(x => x.Id);
            // "Menu Concept*" is required by the 9.3 modal while Database.md 9 keeps the
            // column nullable - the validator enforces the form, the schema does not.
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            // Tenant table (Database.md 9): CompanyId = "For Company*", from the JWT.
            entity.HasIndex(x => x.CompanyId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MenuConceptMenuEntity>(entity =>
        {
            entity.ToTable("PrdMenuConceptMenus");
            entity.HasKey(x => x.Id);
            // Link/unlink keeps the row and flips this status (MenuConceptMenuMappingStatus),
            // so the pair is never deleted - see the filtered unique index below.
            entity.Property(x => x.MappingStatus).IsRequired().HasMaxLength(20);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            entity.HasIndex(x => x.CompanyId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.MenuConceptId);
            entity.HasOne<MenuConceptEntity>()
                .WithMany(x => x.Menus)
                .HasForeignKey(x => x.MenuConceptId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.MenuId);
            entity.HasOne<MenuEntity>()
                .WithMany()
                .HasForeignKey(x => x.MenuId)
                .OnDelete(DeleteBehavior.Restrict);
            // UQ (MenuConceptId, MenuId) among live rows (Database.md 9): a pair may be
            // unlinked and linked again, but only ever as ONE live row - the handlers answer
            // the friendly message instead of letting this fire.
            entity.HasIndex(x => new { x.MenuConceptId, x.MenuId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
        });

        modelBuilder.Entity<ProductEntity>(entity =>
        {
            entity.ToTable("PrdProducts");
            entity.HasKey(x => x.Id);
            // The nullable columns (Database.md 9 without "*") carry the spec form's optional
            // fields at their documented lengths; Name / ManufacturerSupplierId are required
            // by the 9.1 form and are enforced in the validators, not by the schema.
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.Code).HasMaxLength(500);
            entity.Property(x => x.Gtin).HasMaxLength(50);
            entity.Property(x => x.NutritionContentClaims).HasMaxLength(500);
            entity.Property(x => x.PotentialAllergens).HasMaxLength(500);
            entity.Property(x => x.CalorieContent).HasMaxLength(100);
            entity.Property(x => x.AvailableAt).HasMaxLength(200);
            entity.Property(x => x.PackagingSize).HasMaxLength(100);
            entity.Property(x => x.QrCodeKey).HasMaxLength(100);
            entity.Property(x => x.VerifyHalalPublishStatus).HasMaxLength(30);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            // Tenant table (Database.md 9): CompanyId from the JWT, indexed because the global
            // query filter runs on every list. Database.md 9 defines NO unique index - the
            // legacy product-name check is [VERIFY] and was not copied.
            entity.HasIndex(x => x.CompanyId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.SchemeId);
            entity.HasOne<SchemeEntity>()
                .WithMany()
                .HasForeignKey(x => x.SchemeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.ManufacturerSupplierId);
            entity.HasOne<ManufacturerSupplierEntity>()
                .WithMany()
                .HasForeignKey(x => x.ManufacturerSupplierId)
                .OnDelete(DeleteBehavior.Restrict);
            // Three FKs into AdmGeneralData (Brand / Product Category / Marketing Method) -
            // wired without navigations like RawMaterials' two dropdown columns above.
            entity.HasIndex(x => x.BrandId);
            entity.HasOne<GeneralDataEntity>()
                .WithMany()
                .HasForeignKey(x => x.BrandId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.CategoryId);
            entity.HasOne<GeneralDataEntity>()
                .WithMany()
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.MarketingMethodId);
            entity.HasOne<GeneralDataEntity>()
                .WithMany()
                .HasForeignKey(x => x.MarketingMethodId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProductIngredientEntity>(entity =>
        {
            entity.ToTable("PrdProductIngredients");
            entity.HasKey(x => x.Id);
            // The link/unlink toggle of spec 9.1 (Database.md 9): a 20-char status string, not
            // a delete - see ProductIngredientMappingStatus.
            entity.Property(x => x.MappingStatus).IsRequired().HasMaxLength(20);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            entity.HasIndex(x => x.CompanyId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.ProductId);
            entity.HasOne<ProductEntity>()
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.RawMaterialId);
            entity.HasOne<RawMaterialEntity>()
                .WithMany()
                .HasForeignKey(x => x.RawMaterialId)
                .OnDelete(DeleteBehavior.Restrict);
            // UQ (ProductId, RawMaterialId) among live rows (Database.md 9): unlinking keeps the
            // row and flips its status, so the pair can never appear twice - the handlers answer
            // the friendly message instead of letting this fire.
            entity.HasIndex(x => new { x.ProductId, x.RawMaterialId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
        });

        modelBuilder.Entity<ProductImageEntity>(entity =>
        {
            entity.ToTable("PrdProductImages");
            entity.HasKey(x => x.Id);
            // Front / Back / Left / Right stored as their spec string (CodingRules 11).
            entity.Property(x => x.Position).IsRequired().HasMaxLength(20).HasConversion<string>();
            entity.Property(x => x.Version).IsRequired();
            entity.Property(x => x.IsCurrent).IsRequired();
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            entity.HasIndex(x => x.CompanyId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.ProductId);
            entity.HasOne<ProductEntity>()
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            // The replace path reads the four angles of one product on every upload; no unique
            // index, because a replaced row is KEPT (history) next to the current one - only
            // IsCurrent tells them apart (Database.md 9).
            entity.HasIndex(x => new { x.ProductId, x.Position });
            // Required File group (Database.md 9 "_ File"): a row only exists once a file has
            // been uploaded.
            entity.OwnsOne(x => x.Image, image =>
            {
                image.Property(f => f.FileName).IsRequired().HasMaxLength(260);
                image.Property(f => f.StorageKey).IsRequired().HasMaxLength(100);
                image.Property(f => f.ContentType).IsRequired().HasMaxLength(100);
                image.Property(f => f.SizeBytes).IsRequired();
            });
        });

        modelBuilder.Entity<BatchEntity>(entity =>
        {
            entity.ToTable("AppBatches");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(200);
            entity.Property(x => x.CbReferenceNo).HasMaxLength(100);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            entity.HasIndex(x => x.CompanyId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.SchemeId);
            entity.HasOne<SchemeEntity>()
                .WithMany()
                .HasForeignKey(x => x.SchemeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.BrandId);
            entity.HasOne<GeneralDataEntity>()
                .WithMany()
                .HasForeignKey(x => x.BrandId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.ManufacturerSupplierId);
            entity.HasOne<ManufacturerSupplierEntity>()
                .WithMany()
                .HasForeignKey(x => x.ManufacturerSupplierId)
                .OnDelete(DeleteBehavior.Restrict);
            // UQ (CompanyId, Name) among live rows (Database.md 10): "Error! Please provide
            // unique batch name" - the handlers answer the friendly message instead of this.
            entity.HasIndex(x => new { x.CompanyId, x.Name })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
        });

        modelBuilder.Entity<BatchProductEntity>(entity =>
        {
            entity.ToTable("AppBatchProducts");
            entity.HasKey(x => x.Id);
            // Link/unlink toggles MappingStatus (Database.md 10), same as PrdProductIngredients.
            entity.Property(x => x.MappingStatus).IsRequired().HasMaxLength(20);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            entity.HasIndex(x => x.CompanyId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.BatchId);
            entity.HasOne<BatchEntity>()
                .WithMany()
                .HasForeignKey(x => x.BatchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.ProductId);
            entity.HasOne<ProductEntity>()
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            // UQ (BatchId, ProductId) among live rows (Database.md 10).
            entity.HasIndex(x => new { x.BatchId, x.ProductId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
        });

        modelBuilder.Entity<BatchPremiseEntity>(entity =>
        {
            entity.ToTable("AppBatchPremises");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            entity.HasIndex(x => x.CompanyId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.BatchId);
            entity.HasOne<BatchEntity>()
                .WithMany()
                .HasForeignKey(x => x.BatchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.PremiseId);
            entity.HasOne<PremiseEntity>()
                .WithMany()
                .HasForeignKey(x => x.PremiseId)
                .OnDelete(DeleteBehavior.Restrict);
            // UQ (BatchId, PremiseId) among live rows (Database.md 10): unlinking soft-deletes
            // so the slot frees for a re-link.
            entity.HasIndex(x => new { x.BatchId, x.PremiseId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
        });

        modelBuilder.Entity<ApplicationEntity>(entity =>
        {
            entity.ToTable("AppHalalApplications");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ReferenceNo).IsRequired().HasMaxLength(60);
            entity.Property(x => x.ApplicationType).IsRequired().HasMaxLength(20);
            entity.Property(x => x.Status).IsRequired().HasMaxLength(60);
            entity.Property(x => x.CbApplicationNo).HasMaxLength(100);
            entity.Property(x => x.HalalCoachName).HasMaxLength(200);
            entity.Property(x => x.YearlySalesRevenue).HasMaxLength(100);
            entity.Property(x => x.ProductMarket).HasMaxLength(20);
            entity.Property(x => x.AckName).HasMaxLength(200);
            entity.Property(x => x.AckEmail).HasMaxLength(254);
            entity.Property(x => x.AckMobile).HasMaxLength(30);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            entity.HasIndex(x => x.CompanyId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.SchemeId);
            entity.HasOne<SchemeEntity>()
                .WithMany()
                .HasForeignKey(x => x.SchemeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.BatchId);
            entity.HasOne<BatchEntity>()
                .WithMany()
                .HasForeignKey(x => x.BatchId)
                .OnDelete(DeleteBehavior.Restrict);
            // UQ ReferenceNo (Database.md 10): the D-09 generator hands out one number per
            // prefix + scheme + date, so a collision means a bug, not a user mistake.
            entity.HasIndex(x => x.ReferenceNo).IsUnique();
        });

        modelBuilder.Entity<ApplicationStatusHistoryEntity>(entity =>
        {
            entity.ToTable("AppApplicationStatusHistories");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FromStatus).HasMaxLength(60);
            entity.Property(x => x.ToStatus).IsRequired().HasMaxLength(60);
            entity.Property(x => x.Remarks).HasMaxLength(500);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            entity.HasIndex(x => x.CompanyId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.ApplicationId);
            entity.HasOne<ApplicationEntity>()
                .WithMany()
                .HasForeignKey(x => x.ApplicationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ApplicationAdditionalInfoItemEntity>(entity =>
        {
            entity.ToTable("AppApplicationAdditionalInfoItems");
            entity.HasKey(x => x.Id);
            // Section is an enum stored as string (Database.md "Enums"); the widest value
            // today is "QualityControl".
            entity.Property(x => x.Section).IsRequired().HasMaxLength(20);
            entity.Property(x => x.OptionCode).IsRequired().HasMaxLength(50);
            entity.Property(x => x.FreeText).HasMaxLength(500);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            entity.HasIndex(x => x.CompanyId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.ApplicationId);
            entity.HasOne<ApplicationEntity>()
                .WithMany()
                .HasForeignKey(x => x.ApplicationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ApplicationAttachmentEntity>(entity =>
        {
            entity.ToTable("AppApplicationAttachments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            entity.HasIndex(x => x.CompanyId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.ApplicationId);
            entity.HasOne<ApplicationEntity>()
                .WithMany()
                .HasForeignKey(x => x.ApplicationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.DocumentTypeId);
            entity.HasOne(x => x.DocumentType)
                .WithMany()
                .HasForeignKey(x => x.DocumentTypeId)
                .OnDelete(DeleteBehavior.Restrict);
            // Required File group (Database.md 10 "Document File"): a row only exists once a
            // file has been uploaded, exactly like the Raw Material attachment.
            entity.OwnsOne(x => x.Document, document =>
            {
                document.Property(f => f.FileName).IsRequired().HasMaxLength(260);
                document.Property(f => f.StorageKey).IsRequired().HasMaxLength(100);
                document.Property(f => f.ContentType).IsRequired().HasMaxLength(100);
                document.Property(f => f.SizeBytes).IsRequired();
            });
        });

        modelBuilder.Entity<HalalCertificateEntity>(entity =>
        {
            entity.ToTable("AppHalalCertificates");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CertificateNo).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            entity.HasIndex(x => x.CompanyId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.ApplicationId);
            entity.HasOne<ApplicationEntity>()
                .WithMany()
                .HasForeignKey(x => x.ApplicationId)
                .OnDelete(DeleteBehavior.Restrict);
            // UQ (CompanyId, CertificateNo) among live rows (Database.md 10): one number
            // identifies one certificate per company, whichever application holds it.
            entity.HasIndex(x => new { x.CompanyId, x.CertificateNo })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            // Document File is optional (Database.md 10 marks no asterisk): nullable owned
            // columns, same shape as the minutes-meeting attachment document.
            entity.OwnsOne(x => x.Document, document =>
            {
                document.Property(f => f.FileName).IsRequired().HasMaxLength(260);
                document.Property(f => f.StorageKey).IsRequired().HasMaxLength(100);
                document.Property(f => f.ContentType).IsRequired().HasMaxLength(100);
                document.Property(f => f.SizeBytes).IsRequired();
            });
        });

        modelBuilder.Entity<CertificateItemEntity>(entity =>
        {
            entity.ToTable("AppCertificateItems");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ItemName).IsRequired().HasMaxLength(300);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            entity.HasIndex(x => x.CompanyId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.ApplicationId);
            entity.HasOne<ApplicationEntity>()
                .WithMany()
                .HasForeignKey(x => x.ApplicationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.ProductId);
            entity.HasOne<ProductEntity>()
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.PremiseId);
            entity.HasOne<PremiseEntity>()
                .WithMany()
                .HasForeignKey(x => x.PremiseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.BrandId);
            entity.HasOne<GeneralDataEntity>()
                .WithMany()
                .HasForeignKey(x => x.BrandId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.HalalCertificateId);
            entity.HasOne<HalalCertificateEntity>()
                .WithMany()
                .HasForeignKey(x => x.HalalCertificateId)
                .OnDelete(DeleteBehavior.Restrict);
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
            // Null CompanyId = system role, and the tenant filter never applies to this table
            // (Database.md 5), so the column is deliberately not part of an ITenantEntity; the
            // nullable FK to ComCompanies is wired with C-01.
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
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
            // ERD edge ServiceProvider }o--|| Company_Tenant, wired with C-01.
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CountryEntity>()
                .WithMany()
                .HasForeignKey(x => x.CountryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.CountryId);
        });

        modelBuilder.Entity<StaffEntity>(entity =>
        {
            entity.ToTable("ComStaff");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Email).IsRequired().HasMaxLength(254);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(200);
            entity.Property(x => x.IdType).HasMaxLength(50);
            entity.Property(x => x.IdNumber).HasMaxLength(50);
            entity.Property(x => x.EmployeeIdNumber).HasMaxLength(50);
            entity.Property(x => x.Gender).HasMaxLength(20);
            entity.Property(x => x.Religion).HasMaxLength(50);
            entity.Property(x => x.OfficeNumber).HasMaxLength(30);
            entity.Property(x => x.MobileNumber).HasMaxLength(30);
            entity.Property(x => x.TyphoidExpiryDate).HasColumnType("date");
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            // UQ (CompanyId, Email) among live rows (Database.md 3, spec 7.4): a soft delete
            // frees the address, the handler answers the friendly 409 before this could fire.
            entity.HasIndex(x => new { x.CompanyId, x.Email })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => x.CompanyId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Title)
                .WithMany()
                .HasForeignKey(x => x.TitleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.TitleId);
            entity.HasOne(x => x.Department)
                .WithMany()
                .HasForeignKey(x => x.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.DepartmentId);
            entity.HasOne(x => x.Designation)
                .WithMany()
                .HasForeignKey(x => x.DesignationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.DesignationId);
            entity.HasOne(x => x.IhcRole)
                .WithMany()
                .HasForeignKey(x => x.IhcRoleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.IhcRoleId);
            // Photo File? is optional (Database.md 3): nullable owned columns, exactly like
            // AdmUsers.ProfilePicture.
            entity.OwnsOne(x => x.Photo, photo =>
            {
                photo.Property(f => f.FileName).IsRequired().HasMaxLength(260);
                photo.Property(f => f.StorageKey).IsRequired().HasMaxLength(100);
                photo.Property(f => f.ContentType).IsRequired().HasMaxLength(100);
            });
        });

        modelBuilder.Entity<StaffAttachmentEntity>(entity =>
        {
            entity.ToTable("ComStaffAttachments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            entity.HasIndex(x => x.CompanyId);
            entity.HasIndex(x => x.StaffId);
            entity.HasIndex(x => x.DocumentTypeId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Staff)
                .WithMany()
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.DocumentType)
                .WithMany()
                .HasForeignKey(x => x.DocumentTypeId)
                .OnDelete(DeleteBehavior.Restrict);
            // Document* is required (Database.md 3): the NOT NULL File column group.
            entity.OwnsOne(x => x.Document, document =>
            {
                document.Property(f => f.FileName).IsRequired().HasMaxLength(260);
                document.Property(f => f.StorageKey).IsRequired().HasMaxLength(100);
                document.Property(f => f.ContentType).IsRequired().HasMaxLength(100);
            });
        });

        modelBuilder.Entity<TrainingEntity>(entity =>
        {
            entity.ToTable("ComTrainings");
            entity.HasKey(x => x.Id);
            // Enum stored as its spec string (CodingRules 11), e.g. "SlaughtermanHalalChecker".
            entity.Property(x => x.TrainingType).IsRequired().HasMaxLength(30).HasConversion<string>();
            entity.Property(x => x.Name).IsRequired().HasMaxLength(200);
            entity.Property(x => x.TrainingDate).HasColumnType("date");
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            // UQ (CompanyId, Name) among live rows (Database.md 6, spec 7.5 [CODE] "unique
            // training name"): a soft delete frees the name; the handler answers the friendly
            // 409 before this could fire.
            entity.HasIndex(x => new { x.CompanyId, x.Name })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => x.CompanyId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TrainingAttendeeEntity>(entity =>
        {
            entity.ToTable("ComTrainingAttendees");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            // UQ (TrainingId, StaffId) among live rows (Database.md 6): the attendance list is
            // replaced on save, so a removed attendee frees the pair for a later re-pick.
            entity.HasIndex(x => new { x.TrainingId, x.StaffId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => x.CompanyId);
            entity.HasIndex(x => x.StaffId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Training)
                .WithMany()
                .HasForeignKey(x => x.TrainingId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Staff)
                .WithMany()
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TrainingModuleEntity>(entity =>
        {
            entity.ToTable("ComTrainingModules");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ModuleName).IsRequired().HasMaxLength(200);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            entity.HasIndex(x => x.CompanyId);
            entity.HasIndex(x => x.TrainingId);
            entity.HasIndex(x => x.ModuleTypeId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Training)
                .WithMany()
                .HasForeignKey(x => x.TrainingId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ModuleType)
                .WithMany()
                .HasForeignKey(x => x.ModuleTypeId)
                .OnDelete(DeleteBehavior.Restrict);
            // Document File is optional (Database.md 6 marks no asterisk): nullable owned
            // columns, same shape as StaffEntity.Photo.
            entity.OwnsOne(x => x.Document, document =>
            {
                document.Property(f => f.FileName).IsRequired().HasMaxLength(260);
                document.Property(f => f.StorageKey).IsRequired().HasMaxLength(100);
                document.Property(f => f.ContentType).IsRequired().HasMaxLength(100);
            });
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
            // Indexed because login resolves the company list of one user on every sign-in; the
            // FK to ComCompanies is wired with C-01.
            entity.HasIndex(x => x.CompanyId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
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
            // ERD edge WebLink }o--|| Company_Tenant, wired with C-01.
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
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
            // Indexed because the tenant filter runs on every reference-data list; the ERD edge
            // SupportingDocument }o--|| Company_Tenant is wired with C-01.
            entity.HasIndex(x => x.CompanyId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
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

        modelBuilder.Entity<SubscriptionPackageEntity>(entity =>
        {
            entity.ToTable("AdmSubscriptionPackages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).IsRequired().HasMaxLength(20);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            // The 6 §21.5 packages (Database.md 5 "Seeded"); HasData runs once inside the
            // migration, owner edits are never overwritten at startup.
            entity.HasData(SubscriptionPackageSeedData.PackageEntities());
        });

        modelBuilder.Entity<CompanySubscriptionEntity>(entity =>
        {
            entity.ToTable("AdmCompanySubscriptions");
            entity.HasKey(x => x.Id);
            // Enum stored as its Database.md string (CodingRules 11), e.g. "Renewal".
            entity.Property(x => x.EntryType).IsRequired().HasMaxLength(20).HasConversion<string>();
            entity.Property(x => x.Label).HasMaxLength(200);
            entity.Property(x => x.DurationMonths).IsRequired();
            entity.Property(x => x.StartDate).IsRequired().HasColumnType("date");
            entity.Property(x => x.EndDate).IsRequired().HasColumnType("date");
            entity.Property(x => x.SysUserCreated).IsRequired().HasMaxLength(100);
            entity.Property(x => x.SysUserModified).HasMaxLength(100);
            // Indexed because the tenant filter runs on every subscription read; the ERD edges
            // CompanySubscription }o--|| Company_Tenant and "type of" -> AdmSubscriptionPackages
            // are wired here (both Restrict, deletes are always soft - CodingRules 7.1).
            entity.HasIndex(x => x.CompanyId);
            entity.HasOne<CompanyEntity>()
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.PackageId);
            entity.HasOne<SubscriptionPackageEntity>()
                .WithMany()
                .HasForeignKey(x => x.PackageId)
                .OnDelete(DeleteBehavior.Restrict);
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

            // RawMaterials and PrdMenus carry the special "Accessible For" rule of CodingRules
            // 7.3 and are configured after the loop.
            if (entityType.ClrType == typeof(RawMaterialEntity)
                || entityType.ClrType == typeof(MenuEntity))
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

        // CodingRules 7.3 / spec 10.2: a raw material is visible to its owner company, to a
        // company listed in RawMaterialAccessibleCompanies ("Accessible For", >= 1 row) and to a
        // Switch Company = ALL token - in handlers as here, nowhere else.
        modelBuilder.Entity<RawMaterialEntity>().HasQueryFilter(row =>
            !row.IsDeleted
            && (CurrentIsPlatformAdminViewAll
                || row.CompanyId == CurrentCompanyId
                || row.AccessibleCompanies.Any(company =>
                    !company.IsDeleted && company.AccessibleCompanyId == CurrentCompanyId)));

        // CodingRules 7.3 / spec 9.2: a menu is visible to its owner company, to a company
        // listed in PrdMenuAccessibleCompanies ("List of Company", >= 1 row) and to a Switch
        // Company = ALL token - in handlers as here, nowhere else.
        modelBuilder.Entity<MenuEntity>().HasQueryFilter(row =>
            !row.IsDeleted
            && (CurrentIsPlatformAdminViewAll
                || row.CompanyId == CurrentCompanyId
                || row.AccessibleCompanies.Any(company =>
                    !company.IsDeleted && company.AccessibleCompanyId == CurrentCompanyId)));
    }
}
