using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Payment.CertificatePayment;
using VHSmart_Api.Shared.Domain.Payment;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Payment.CertificatePayment.CertificatePaymentTestData;

namespace VHSmart_Api.Tests.Features.Payment.CertificatePayment;

public class CertificatePaymentWorkflowTests
{
    private static CreateCertificatePaymentCommand CreateCommand(
        Guid applicationId,
        Guid staffId,
        decimal amount = 500m,
        string? paymentVoucherNo = null) => new(
        applicationId,
        DateHalalApplication: new DateOnly(2026, 10, 1),
        ChargeLetterDate: new DateOnly(2026, 10, 5),
        Amount: amount,
        ReferenceNo: "REF-1",
        BankName: "Bank Islam",
        BankAccountNo: "1234-5678",
        SubmittedByStaffId: staffId,
        SubmittedDate: new DateOnly(2026, 10, 6),
        ReceivedByStaffId: staffId,
        ReceivedDate: new DateOnly(2026, 10, 7),
        PaymentVoucherNo: paymentVoucherNo);

    private static UpdateCertificatePaymentCommand UpdateCommand(
        Guid id,
        Guid staffId,
        string status,
        decimal amount = 500m) => new(
        id,
        DateHalalApplication: new DateOnly(2026, 10, 1),
        ChargeLetterDate: new DateOnly(2026, 10, 5),
        Amount: amount,
        ReferenceNo: "REF-1",
        BankName: "Bank Islam",
        BankAccountNo: "1234-5678",
        SubmittedByStaffId: staffId,
        SubmittedDate: new DateOnly(2026, 10, 6),
        ReceivedByStaffId: staffId,
        ReceivedDate: new DateOnly(2026, 10, 7),
        PaymentVoucherNo: "PV-1",
        Status: status);

    [Fact]
    public async Task Create_SavesRowWithInitialStatusAndDetail()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCertificationBodyAsync(db);
        var schemeId = await SeedSchemeAsync(db);
        var applicationId = await SeedApplicationAsync(
            db, CompanyA, schemeId, cbApplicationNo: "CB-001");
        var staffId = await SeedStaffAsync(db, CompanyA);

        var detail = await new CreateCertificatePaymentHandler(db, user).Handle(
            CreateCommand(applicationId, staffId), CancellationToken.None);

        Assert.Equal(PaymentStatus.RequestBankDraft, detail.Status);
        Assert.Equal(applicationId, detail.HalalApplicationId);
        Assert.Equal($"HAL({schemeId:N})/12102026/1", detail.ApplicationReferenceNo);
        Assert.Equal("CB-001", detail.CbApplicationNo);
        Assert.Equal("Halal Food", detail.Scheme);
        Assert.Equal("JABATAN KEMAJUAN ISLAM MALAYSIA (JAKIM)", detail.CertificationBody);
        Assert.Equal("Staff One", detail.SubmittedBy);
        Assert.Equal("Staff One", detail.ReceivedBy);
        Assert.Equal(500m, detail.Amount);
        Assert.Null(detail.PaymentVoucherNo);
        Assert.True(await db.Payments.OfType<CertificatePaymentEntity>()
            .AnyAsync(row => row.Id == detail.Id));
    }

    [Fact]
    public async Task CreateValidator_AmountZero_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var schemeId = await SeedSchemeAsync(db);
        var applicationId = await SeedApplicationAsync(db, CompanyA, schemeId);
        var staffId = await SeedStaffAsync(db, CompanyA);

        var result = await new CreateCertificatePaymentValidator(db, user)
            .ValidateAsync(CreateCommand(applicationId, staffId, amount: 0m));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.ErrorMessage == "Amount must be greater than 0.");
    }

    [Fact]
    public async Task CreateValidator_ForeignApplication_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var schemeId = await SeedSchemeAsync(db);
        var foreignApplication = await SeedApplicationAsync(db, CompanyB, schemeId);
        var staffId = await SeedStaffAsync(db, CompanyA);

        var result = await new CreateCertificatePaymentValidator(db, user)
            .ValidateAsync(CreateCommand(foreignApplication, staffId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.ErrorMessage == "Halal Application not found.");
    }

    [Fact]
    public async Task CreateValidator_ForeignStaff_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var schemeId = await SeedSchemeAsync(db);
        var applicationId = await SeedApplicationAsync(db, CompanyA, schemeId);
        var foreignStaff = await SeedStaffAsync(db, CompanyB);

        var result = await new CreateCertificatePaymentValidator(db, user)
            .ValidateAsync(CreateCommand(applicationId, foreignStaff));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.ErrorMessage == "Submitted By not found.");
        Assert.Contains(result.Errors, error =>
            error.ErrorMessage == "Received By not found.");
    }

    [Fact]
    public async Task CreateValidator_MissingSubmittedDate_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var schemeId = await SeedSchemeAsync(db);
        var applicationId = await SeedApplicationAsync(db, CompanyA, schemeId);
        var staffId = await SeedStaffAsync(db, CompanyA);

        var command = CreateCommand(applicationId, staffId) with
        {
            SubmittedDate = default
        };
        var result = await new CreateCertificatePaymentValidator(db, user).ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.ErrorMessage == "Submitted Date is required.");
    }

    [Fact]
    public async Task Update_AdvancesOneStep_Forward()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (_, _, staffId, paymentId) = await SeedStackAsync(db, CompanyA);

        var detail = await new UpdateCertificatePaymentHandler(db, user).Handle(
            UpdateCommand(paymentId, staffId, PaymentStatus.ReceivedBankDraft),
            CancellationToken.None);

        Assert.Equal(PaymentStatus.ReceivedBankDraft, detail.Status);
    }

    [Fact]
    public async Task Update_SameStatus_SavesFieldsWithoutTransition()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (_, _, staffId, paymentId) = await SeedStackAsync(db, CompanyA);

        var detail = await new UpdateCertificatePaymentHandler(db, user).Handle(
            UpdateCommand(paymentId, staffId, PaymentStatus.RequestBankDraft, amount: 750m),
            CancellationToken.None);

        Assert.Equal(PaymentStatus.RequestBankDraft, detail.Status);
        Assert.Equal(750m, detail.Amount);
    }

    [Fact]
    public async Task Update_SkippingStep_IsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (_, _, staffId, paymentId) = await SeedStackAsync(db, CompanyA);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            new UpdateCertificatePaymentHandler(db, user).Handle(
                UpdateCommand(paymentId, staffId, PaymentStatus.PaymentToCb),
                CancellationToken.None));

        Assert.Equal("Payment status can only advance one step at a time.", exception.Message);
    }

    [Fact]
    public async Task Update_Reverting_IsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (_, _, staffId, paymentId) = await SeedStackAsync(
            db, CompanyA, PaymentStatus.ReceivedBankDraft);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            new UpdateCertificatePaymentHandler(db, user).Handle(
                UpdateCommand(paymentId, staffId, PaymentStatus.RequestBankDraft),
                CancellationToken.None));

        Assert.Equal("Payment status can only advance one step at a time.", exception.Message);
    }

    [Fact]
    public async Task Update_WalksChainToCompleted()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (_, _, staffId, paymentId) = await SeedStackAsync(db, CompanyA);

        var handler = new UpdateCertificatePaymentHandler(db, user);
        var received = await handler.Handle(
            UpdateCommand(paymentId, staffId, PaymentStatus.ReceivedBankDraft),
            CancellationToken.None);
        var paid = await handler.Handle(
            UpdateCommand(paymentId, staffId, PaymentStatus.PaymentToCb),
            CancellationToken.None);
        var completed = await handler.Handle(
            UpdateCommand(paymentId, staffId, PaymentStatus.Completed),
            CancellationToken.None);

        Assert.Equal(PaymentStatus.ReceivedBankDraft, received.Status);
        Assert.Equal(PaymentStatus.PaymentToCb, paid.Status);
        Assert.Equal(PaymentStatus.Completed, completed.Status);
    }

    [Fact]
    public async Task Update_Completed_IsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (_, _, staffId, paymentId) = await SeedStackAsync(db, CompanyA, PaymentStatus.Completed);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            new UpdateCertificatePaymentHandler(db, user).Handle(
                UpdateCommand(paymentId, staffId, PaymentStatus.Completed),
                CancellationToken.None));

        Assert.Equal("Completed payments cannot be edited.", exception.Message);
    }

    [Fact]
    public async Task Update_Unknown_IsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (_, _, staffId, _) = await SeedStackAsync(db, CompanyA);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateCertificatePaymentHandler(db, user).Handle(
                UpdateCommand(Guid.NewGuid(), staffId, PaymentStatus.ReceivedBankDraft),
                CancellationToken.None));
    }

    [Fact]
    public async Task Update_Foreign_IsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var schemeId = await SeedSchemeAsync(db);
        var foreignApplication = await SeedApplicationAsync(db, CompanyB, schemeId);
        var foreignStaff = await SeedStaffAsync(db, CompanyB);
        var foreignPayment = await SeedPaymentAsync(
            db, CompanyB, foreignApplication, foreignStaff, foreignStaff);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateCertificatePaymentHandler(db, user).Handle(
                UpdateCommand(foreignPayment, foreignStaff, PaymentStatus.ReceivedBankDraft),
                CancellationToken.None));
    }

    [Fact]
    public async Task UpdateValidator_ForeignStaff_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (_, _, _, paymentId) = await SeedStackAsync(db, CompanyA);
        var foreignStaff = await SeedStaffAsync(db, CompanyB);

        var result = await new UpdateCertificatePaymentValidator(db, user)
            .ValidateAsync(UpdateCommand(paymentId, foreignStaff, PaymentStatus.Completed));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.ErrorMessage == "Submitted By not found.");
    }

    [Fact]
    public async Task Delete_SoftDeletesPaymentAndAttachments()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (_, _, _, paymentId) = await SeedStackAsync(db, CompanyA);
        var attachmentId = await SeedAttachmentAsync(db, CompanyA, paymentId);

        await new DeleteCertificatePaymentHandler(db, user).Handle(
            new DeleteCertificatePaymentCommand(paymentId), CancellationToken.None);

        var payment = await db.Payments.IgnoreQueryFilters()
            .OfType<CertificatePaymentEntity>()
            .FirstAsync(row => row.Id == paymentId);
        var attachment = await db.PaymentAttachments.IgnoreQueryFilters()
            .FirstAsync(row => row.Id == attachmentId);
        Assert.True(payment.IsDeleted);
        Assert.True(attachment.IsDeleted);
    }

    [Fact]
    public async Task Delete_Completed_IsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (_, _, _, paymentId) = await SeedStackAsync(db, CompanyA, PaymentStatus.Completed);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            new DeleteCertificatePaymentHandler(db, user).Handle(
                new DeleteCertificatePaymentCommand(paymentId), CancellationToken.None));

        Assert.Equal("Completed payments cannot be deleted.", exception.Message);
    }

    [Fact]
    public async Task Delete_Foreign_IsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var schemeId = await SeedSchemeAsync(db);
        var foreignApplication = await SeedApplicationAsync(db, CompanyB, schemeId);
        var foreignStaff = await SeedStaffAsync(db, CompanyB);
        var foreignPayment = await SeedPaymentAsync(
            db, CompanyB, foreignApplication, foreignStaff, foreignStaff);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteCertificatePaymentHandler(db, user).Handle(
                new DeleteCertificatePaymentCommand(foreignPayment), CancellationToken.None));
    }

    [Fact]
    public async Task GetById_ReturnsDetailWithReadThroughs()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCertificationBodyAsync(db);
        var (schemeId, _, _, paymentId) = await SeedStackAsync(db, CompanyA);

        var detail = await new GetCertificatePaymentByIdHandler(db, user).Handle(
            new GetCertificatePaymentByIdQuery(paymentId), CancellationToken.None);

        Assert.Equal(paymentId, detail.Id);
        Assert.Equal($"HAL({schemeId:N})/12102026/1", detail.ApplicationReferenceNo);
        Assert.Equal("CB-001", detail.CbApplicationNo);
        Assert.Equal("Halal Food", detail.Scheme);
        Assert.Equal("DRAFT", detail.ApplicationStatus);
        Assert.Equal("Staff One", detail.SubmittedBy);
        Assert.Equal("JABATAN KEMAJUAN ISLAM MALAYSIA (JAKIM)", detail.CertificationBody);
        Assert.NotEqual(default, detail.CreatedDate);
    }

    [Fact]
    public async Task GetById_Unknown_IsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetCertificatePaymentByIdHandler(db, user).Handle(
                new GetCertificatePaymentByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task GetById_Foreign_IsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var schemeId = await SeedSchemeAsync(db);
        var foreignApplication = await SeedApplicationAsync(db, CompanyB, schemeId);
        var foreignStaff = await SeedStaffAsync(db, CompanyB);
        var foreignPayment = await SeedPaymentAsync(
            db, CompanyB, foreignApplication, foreignStaff, foreignStaff);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetCertificatePaymentByIdHandler(db, user).Handle(
                new GetCertificatePaymentByIdQuery(foreignPayment), CancellationToken.None));
    }
}
