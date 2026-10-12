namespace VHSmart_Api.Shared.Domain.Payment;

// Which of the two payment screens owns the row (Database.md 12, TPH discriminator
// PaymentType). Stored as string so the column reads "Certificate" / "Other".
public enum PaymentType
{
    Certificate,
    Other
}

// The workflow statuses of spec 15.1 / 15.2 verbatim (D-23 stores display strings).
public static class PaymentStatus
{
    public const string RequestBankDraft = "REQUEST BANK DRAFT TO FINANCE DEPT";

    public const string ReceivedBankDraft = "RECEIVED BANK DRAFT FROM FINANCE DEPT";

    public const string PaymentToCb = "PAYMENT TO CB";

    public const string PaymentToServiceProvider = "PAYMENT TO SERVICE PROVIDER";

    public const string Completed = "COMPLETED";
}

// D-23: any user with Payment.* Edit may move the status forward ONE step at a time; no
// revert; COMPLETED rows are read-only. One class holds the transition map for both flows
// (15.1 certificate, 15.2 other) so the rule can change later in a single place. The first
// two steps are shared; step 3 differs (CB vs Service Provider).
public static class PaymentStatusPolicy
{
    public static string InitialStatus(PaymentType type) => type switch
    {
        PaymentType.Certificate => PaymentStatus.RequestBankDraft,
        _ => PaymentStatus.RequestBankDraft
    };

    // The single next step, or null when the row is at COMPLETED (or the status is
    // unknown - treated as not advanceable rather than thrown).
    public static string? Next(PaymentType type, string current) => current switch
    {
        PaymentStatus.RequestBankDraft => PaymentStatus.ReceivedBankDraft,
        PaymentStatus.ReceivedBankDraft => type == PaymentType.Certificate
            ? PaymentStatus.PaymentToCb
            : PaymentStatus.PaymentToServiceProvider,
        PaymentStatus.PaymentToCb or PaymentStatus.PaymentToServiceProvider
            when type == PaymentType.Certificate
                || current == PaymentStatus.PaymentToServiceProvider =>
            PaymentStatus.Completed,
        _ => null
    };

    public static bool CanAdvance(PaymentType type, string from, string to) =>
        Next(type, from) == to;

    public static bool IsReadOnly(string status) => status == PaymentStatus.Completed;
}

// Payment > Certificate (spec 15.1) / Payment > Other (15.2): Database.md 12 describes ONE
// tenant table PayPayments mapped TPH with the PaymentType discriminator - this base holds
// the common columns and PaymentEntity-derived rows arrive per screen. CB / CB ref / scheme /
// type / brand are NEVER copied here: the list and form read them through HalalApplicationId
// (Database.md 12 note). Amount is typed by the user and PaymentVoucherNo is an optional
// typed value (D-23). Submitted/Received staff + dates are required from creation
// (Database.md 12 markers).
public abstract class PaymentEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public string Status { get; set; } = PaymentStatus.RequestBankDraft;

    public decimal Amount { get; set; }

    public string? ReferenceNo { get; set; }

    public string? BankName { get; set; }

    public string? BankAccountNo { get; set; }

    public Guid SubmittedByStaffId { get; set; }

    public DateOnly SubmittedDate { get; set; }

    public Guid ReceivedByStaffId { get; set; }

    public DateOnly ReceivedDate { get; set; }

    public string? PaymentVoucherNo { get; set; }
}

// The Certificate-only columns of Database.md 12. HalalApplicationId is the VH SMART
// Reference No. link: selecting an application is what fills the read-through list columns.
// The application link never changes after creation (flagged: spec silent; changing it would
// orphan the read-throughs the whole screen is built on).
public class CertificatePaymentEntity : PaymentEntity
{
    public Guid HalalApplicationId { get; set; }

    public DateOnly? DateHalalApplication { get; set; }

    public DateOnly? ChargeLetterDate { get; set; }
}
