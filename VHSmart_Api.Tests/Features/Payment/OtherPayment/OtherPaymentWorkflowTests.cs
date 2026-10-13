using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Payment.OtherPayment;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Payment;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Payment.OtherPayment.OtherPaymentTestData;

namespace VHSmart_Api.Tests.Features.Payment.OtherPayment;

public class OtherPaymentWorkflowTests
{
    private static CreateOtherPaymentCommand CreateCommand(
        Guid categoryId,
        Guid payeeId,
        Guid staffId,
        decimal amount = 150m,
        string? paymentVoucherNo = null) => new(
        categoryId,
        payeeId,
        Amount: amount,
        ReferenceNo: "REF-1",
        BankName: "Bank Islam",
        BankAccountNo: "1234-5678",
        SubmittedByStaffId: staffId,
        SubmittedDate: new DateOnly(2026, 10, 6),
        ReceivedByStaffId: staffId,
        ReceivedDate: new DateOnly(2026, 10, 7),
        PaymentVoucherNo: paymentVoucherNo);

    private static UpdateOtherPaymentCommand UpdateCommand(
        Guid id,
        Guid categoryId,
        Guid payeeId,
        Guid staffId,
        string status,
        decimal amount = 150m) => new(
        id,
        categoryId,
        payeeId,
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
        var categoryId = await SeedPaymentCategoryAsync(db, CompanyA);
        var payeeId = await SeedServiceProviderAsync(
            db, CompanyA, bankName: "MAYBANK", bankAccountNo: "1111-2222");
        var staffId = await SeedStaffAsync(db, CompanyA);

        var detail = await new CreateOtherPaymentHandler(db, user).Handle(
            CreateCommand(categoryId, payeeId, staffId), CancellationToken.None);

        Assert.Equal(PaymentStatus.RequestBankDraft, detail.Status);
        Assert.Equal(categoryId, detail.PaymentCategoryId);
        Assert.Equal("CATEGORY A", detail.PaymentCategory);
        Assert.Equal(payeeId, detail.ServiceProviderId);
        Assert.Equal("SERUNAI COMMERCE", detail.Payee);
        Assert.Equal("MAYBANK", detail.PayeeBankName);
        Assert.Equal("1111-2222", detail.PayeeBankAccountNo);
        Assert.Equal("Staff One", detail.SubmittedBy);
        Assert.Equal("Staff One", detail.ReceivedBy);
        Assert.Equal(150m, detail.Amount);
        Assert.Null(detail.PaymentVoucherNo);
        Assert.True(await db.Payments.OfType<OtherPaymentEntity>()
            .AnyAsync(row => row.Id == detail.Id));
    }

    [Fact]
    public async Task CreateValidator_AmountZero_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var categoryId = await SeedPaymentCategoryAsync(db, CompanyA);
        var payeeId = await SeedServiceProviderAsync(db, CompanyA);
        var staffId = await SeedStaffAsync(db, CompanyA);

        var result = await new CreateOtherPaymentValidator(db, user)
            .ValidateAsync(CreateCommand(categoryId, payeeId, staffId, amount: 0m));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.ErrorMessage == "Amount must be greater than 0.");
    }

    [Fact]
    public async Task CreateValidator_ForeignCategory_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignCategoryId = await SeedPaymentCategoryAsync(db, CompanyB);
        var payeeId = await SeedServiceProviderAsync(db, CompanyA);
        var staffId = await SeedStaffAsync(db, CompanyA);

        var result = await new CreateOtherPaymentValidator(db, user)
            .ValidateAsync(CreateCommand(foreignCategoryId, payeeId, staffId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.ErrorMessage == "Payment Category not found.");
    }

    [Fact]
    public async Task CreateValidator_CategoryOutsidePaymentGroup_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var wrongGroup = new GeneralDataEntity
        {
            CompanyId = CompanyA,
            Group = GeneralDataGroup.PRODUCT,
            Category = "Payment Category",
            Name = "CATEGORY A"
        };
        db.GeneralData.Add(wrongGroup);
        await db.SaveChangesAsync();
        var payeeId = await SeedServiceProviderAsync(db, CompanyA);
        var staffId = await SeedStaffAsync(db, CompanyA);

        var result = await new CreateOtherPaymentValidator(db, user)
            .ValidateAsync(CreateCommand(wrongGroup.Id, payeeId, staffId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.ErrorMessage == "Payment Category not found.");
    }

    [Fact]
    public async Task CreateValidator_ForeignPayee_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var categoryId = await SeedPaymentCategoryAsync(db, CompanyA);
        var foreignPayee = await SeedServiceProviderAsync(db, CompanyB);
        var staffId = await SeedStaffAsync(db, CompanyA);

        var result = await new CreateOtherPaymentValidator(db, user)
            .ValidateAsync(CreateCommand(categoryId, foreignPayee, staffId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.ErrorMessage == "Payee not found.");
    }

    [Fact]
    public async Task CreateValidator_ForeignStaff_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var categoryId = await SeedPaymentCategoryAsync(db, CompanyA);
        var payeeId = await SeedServiceProviderAsync(db, CompanyA);
        var foreignStaff = await SeedStaffAsync(db, CompanyB);

        var result = await new CreateOtherPaymentValidator(db, user)
            .ValidateAsync(CreateCommand(categoryId, payeeId, foreignStaff));

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
        var categoryId = await SeedPaymentCategoryAsync(db, CompanyA);
        var payeeId = await SeedServiceProviderAsync(db, CompanyA);
        var staffId = await SeedStaffAsync(db, CompanyA);

        var command = CreateCommand(categoryId, payeeId, staffId) with
        {
            SubmittedDate = default
        };
        var result = await new CreateOtherPaymentValidator(db, user).ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.ErrorMessage == "Submitted Date is required.");
    }

    [Fact]
    public async Task Update_AdvancesOneStep_Forward()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (categoryId, payeeId, staffId, paymentId) = await SeedStackAsync(db, CompanyA);

        var detail = await new UpdateOtherPaymentHandler(db, user).Handle(
            UpdateCommand(paymentId, categoryId, payeeId, staffId,
                PaymentStatus.ReceivedBankDraft),
            CancellationToken.None);

        Assert.Equal(PaymentStatus.ReceivedBankDraft, detail.Status);
    }

    [Fact]
    public async Task Update_SameStatus_SavesFieldsWithoutTransition()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (categoryId, payeeId, staffId, paymentId) = await SeedStackAsync(db, CompanyA);

        var detail = await new UpdateOtherPaymentHandler(db, user).Handle(
            UpdateCommand(paymentId, categoryId, payeeId, staffId,
                PaymentStatus.RequestBankDraft, amount: 250m),
            CancellationToken.None);

        Assert.Equal(PaymentStatus.RequestBankDraft, detail.Status);
        Assert.Equal(250m, detail.Amount);
    }

    [Fact]
    public async Task Update_SkippingStep_IsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (categoryId, payeeId, staffId, paymentId) = await SeedStackAsync(db, CompanyA);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            new UpdateOtherPaymentHandler(db, user).Handle(
                UpdateCommand(paymentId, categoryId, payeeId, staffId,
                    PaymentStatus.PaymentToServiceProvider),
                CancellationToken.None));

        Assert.Equal("Payment status can only advance one step at a time.", exception.Message);
    }

    [Fact]
    public async Task Update_ToCertificateStep_IsConflict()
    {
        // The Other chain branches to PAYMENT TO SERVICE PROVIDER - PAYMENT TO CB belongs to
        // the Certificate screen only (spec 15.2 step 3).
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (categoryId, payeeId, staffId, paymentId) = await SeedStackAsync(
            db, CompanyA, PaymentStatus.ReceivedBankDraft);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            new UpdateOtherPaymentHandler(db, user).Handle(
                UpdateCommand(paymentId, categoryId, payeeId, staffId,
                    PaymentStatus.PaymentToCb),
                CancellationToken.None));

        Assert.Equal("Payment status can only advance one step at a time.", exception.Message);
    }

    [Fact]
    public async Task Update_Reverting_IsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (categoryId, payeeId, staffId, paymentId) = await SeedStackAsync(
            db, CompanyA, PaymentStatus.ReceivedBankDraft);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            new UpdateOtherPaymentHandler(db, user).Handle(
                UpdateCommand(paymentId, categoryId, payeeId, staffId,
                    PaymentStatus.RequestBankDraft),
                CancellationToken.None));

        Assert.Equal("Payment status can only advance one step at a time.", exception.Message);
    }

    [Fact]
    public async Task Update_WalksChainToCompleted()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (categoryId, payeeId, staffId, paymentId) = await SeedStackAsync(db, CompanyA);

        var handler = new UpdateOtherPaymentHandler(db, user);
        var received = await handler.Handle(
            UpdateCommand(paymentId, categoryId, payeeId, staffId,
                PaymentStatus.ReceivedBankDraft),
            CancellationToken.None);
        var paid = await handler.Handle(
            UpdateCommand(paymentId, categoryId, payeeId, staffId,
                PaymentStatus.PaymentToServiceProvider),
            CancellationToken.None);
        var completed = await handler.Handle(
            UpdateCommand(paymentId, categoryId, payeeId, staffId,
                PaymentStatus.Completed),
            CancellationToken.None);

        Assert.Equal(PaymentStatus.ReceivedBankDraft, received.Status);
        Assert.Equal(PaymentStatus.PaymentToServiceProvider, paid.Status);
        Assert.Equal(PaymentStatus.Completed, completed.Status);
    }

    [Fact]
    public async Task Update_Completed_IsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (categoryId, payeeId, staffId, paymentId) = await SeedStackAsync(
            db, CompanyA, PaymentStatus.Completed);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            new UpdateOtherPaymentHandler(db, user).Handle(
                UpdateCommand(paymentId, categoryId, payeeId, staffId,
                    PaymentStatus.Completed),
                CancellationToken.None));

        Assert.Equal("Completed payments cannot be edited.", exception.Message);
    }

    [Fact]
    public async Task Update_Unknown_IsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (categoryId, payeeId, staffId, _) = await SeedStackAsync(db, CompanyA);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateOtherPaymentHandler(db, user).Handle(
                UpdateCommand(Guid.NewGuid(), categoryId, payeeId, staffId,
                    PaymentStatus.ReceivedBankDraft),
                CancellationToken.None));
    }

    [Fact]
    public async Task Update_Foreign_IsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var categoryId = await SeedPaymentCategoryAsync(db, CompanyB);
        var payeeId = await SeedServiceProviderAsync(db, CompanyB);
        var foreignStaff = await SeedStaffAsync(db, CompanyB);
        var foreignPayment = await SeedOtherPaymentAsync(
            db, CompanyB, categoryId, payeeId, foreignStaff);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateOtherPaymentHandler(db, user).Handle(
                UpdateCommand(foreignPayment, categoryId, payeeId, foreignStaff,
                    PaymentStatus.ReceivedBankDraft),
                CancellationToken.None));
    }

    [Fact]
    public async Task UpdateValidator_ForeignCategory_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (_, payeeId, staffId, paymentId) = await SeedStackAsync(db, CompanyA);
        var foreignCategoryId = await SeedPaymentCategoryAsync(db, CompanyB);

        var result = await new UpdateOtherPaymentValidator(db, user)
            .ValidateAsync(UpdateCommand(
                paymentId, foreignCategoryId, payeeId, staffId, PaymentStatus.Completed));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.ErrorMessage == "Payment Category not found.");
    }

    [Fact]
    public async Task Delete_SoftDeletesPayment()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (_, _, _, paymentId) = await SeedStackAsync(db, CompanyA);

        await new DeleteOtherPaymentHandler(db, user).Handle(
            new DeleteOtherPaymentCommand(paymentId), CancellationToken.None);

        var payment = await db.Payments.IgnoreQueryFilters()
            .OfType<OtherPaymentEntity>()
            .FirstAsync(row => row.Id == paymentId);
        Assert.True(payment.IsDeleted);
    }

    [Fact]
    public async Task Delete_Completed_IsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (_, _, _, paymentId) = await SeedStackAsync(db, CompanyA, PaymentStatus.Completed);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            new DeleteOtherPaymentHandler(db, user).Handle(
                new DeleteOtherPaymentCommand(paymentId), CancellationToken.None));

        Assert.Equal("Completed payments cannot be deleted.", exception.Message);
    }

    [Fact]
    public async Task Delete_Foreign_IsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var categoryId = await SeedPaymentCategoryAsync(db, CompanyB);
        var payeeId = await SeedServiceProviderAsync(db, CompanyB);
        var foreignStaff = await SeedStaffAsync(db, CompanyB);
        var foreignPayment = await SeedOtherPaymentAsync(
            db, CompanyB, categoryId, payeeId, foreignStaff);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteOtherPaymentHandler(db, user).Handle(
                new DeleteOtherPaymentCommand(foreignPayment), CancellationToken.None));
    }

    [Fact]
    public async Task GetById_ReturnsDetailWithReadThroughs()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (categoryId, payeeId, _, paymentId) = await SeedStackAsync(db, CompanyA);

        var detail = await new GetOtherPaymentByIdHandler(db, user).Handle(
            new GetOtherPaymentByIdQuery(paymentId), CancellationToken.None);

        Assert.Equal(paymentId, detail.Id);
        Assert.Equal(categoryId, detail.PaymentCategoryId);
        Assert.Equal("CATEGORY A", detail.PaymentCategory);
        Assert.Equal(payeeId, detail.ServiceProviderId);
        Assert.Equal("SERUNAI COMMERCE", detail.Payee);
        Assert.Equal("MAYBANK", detail.PayeeBankName);
        Assert.Equal("1234-5678", detail.PayeeBankAccountNo);
        Assert.Equal("Staff One", detail.SubmittedBy);
        Assert.NotEqual(default, detail.CreatedDate);
    }

    [Fact]
    public async Task GetById_Unknown_IsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetOtherPaymentByIdHandler(db, user).Handle(
                new GetOtherPaymentByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task GetById_Foreign_IsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var categoryId = await SeedPaymentCategoryAsync(db, CompanyB);
        var payeeId = await SeedServiceProviderAsync(db, CompanyB);
        var foreignStaff = await SeedStaffAsync(db, CompanyB);
        var foreignPayment = await SeedOtherPaymentAsync(
            db, CompanyB, categoryId, payeeId, foreignStaff);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetOtherPaymentByIdHandler(db, user).Handle(
                new GetOtherPaymentByIdQuery(foreignPayment), CancellationToken.None));
    }
}
