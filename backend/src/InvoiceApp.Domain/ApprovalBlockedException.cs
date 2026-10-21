namespace InvoiceApp.Domain;

/// <summary>Indicates an approval attempt was blocked by an approval eligibility rule.</summary>
public sealed class ApprovalBlockedException : Exception
{
    public ApprovalBlockedException(string message) : base(message)
    {
    }
}
