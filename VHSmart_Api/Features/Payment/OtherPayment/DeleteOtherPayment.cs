using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Payment;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Payment.OtherPayment;

// Delete of an Other payment (spec 15.2 list Action - the list shows view + delete; the
// missing edit icon is the completed-rows-are-locked reading of [VERIFY], already enforced by
// the D-23 read-only rule). Soft delete (CodingRules 7.1). The Payment > Other task's Tables
// column carries only PayPayments, so unlike the Certificate delete there are no attachment
// children to remove here (flagged - the spec form does show a "List of Attachments" dialog,
// same as the Certificate form; it belongs to PayPaymentAttachments and needs the owner's
// go-ahead). COMPLETED rows are read-only (D-23) - 409. Unknown/foreign -> 404. Behind
// Payment.Other Delete.
public record DeleteOtherPaymentCommand(Guid Id) : IRequest;

public class DeleteOtherPaymentValidator
    : AbstractValidator<DeleteOtherPaymentCommand>
{
    public DeleteOtherPaymentValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required.");
    }
}

public class DeleteOtherPaymentHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<DeleteOtherPaymentCommand>
{
    public async Task Handle(DeleteOtherPaymentCommand request, CancellationToken ct)
    {
        var payment = await OtherPaymentQueries.Own(db, user)
                .FirstOrDefaultAsync(row => row.Id == request.Id, ct)
            ?? throw new NotFoundException("Other payment not found.");

        if (PaymentStatusPolicy.IsReadOnly(payment.Status))
        {
            throw new ConflictException("Completed payments cannot be deleted.");
        }

        db.Payments.Remove(payment);

        await db.SaveChangesAsync(ct);
    }
}
