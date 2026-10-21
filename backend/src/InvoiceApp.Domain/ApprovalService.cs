namespace InvoiceApp.Domain;

/// <summary>
/// Moves an invoice through its lifecycle: Draft -> PendingApproval -> Approved or Rejected.
/// High-value invoices require two distinct approvers to complete approval.
/// </summary>
public class ApprovalService
{
    private readonly IInvoiceRepository _repository;

    public ApprovalService(IInvoiceRepository repository)
    {
        _repository = repository;
    }

    public Invoice Submit(string invoiceId)
    {
        var invoice = Find(invoiceId);

        if (invoice.Status != InvoiceStatus.Draft)
        {
            throw new InvalidOperationException(
                $"Invoice {invoiceId} is {invoice.Status}; only drafts can be submitted.");
        }

        invoice.Status = InvoiceStatus.PendingApproval;
        _repository.Update(invoice);
        return invoice;
    }

    public Invoice Approve(string invoiceId, string approver, string role)
    {
        var invoice = Find(invoiceId);
        EnsurePending(invoice);

        if (role is not ("Manager" or "Director"))
        {
            throw new ApprovalBlockedException("Only Manager or Director may approve invoices.");
        }

        if (invoice.Amount <= 500_000.00m)
        {
            if (role == "Manager" && invoice.Amount > 50_000.00m)
            {
                throw new ApprovalBlockedException("Manager may approve invoices up to 50,000.00.");
            }

            invoice.Status = InvoiceStatus.Approved;
            invoice.DecidedBy = approver;
            invoice.DecidedAt = DateTime.UtcNow;
            _repository.Update(invoice);
            return invoice;
        }

        if (invoice.FirstApprovedBy is null)
        {
            invoice.FirstApprovedBy = approver;
            invoice.FirstApprovedAt = DateTime.UtcNow;
            _repository.Update(invoice);
            return invoice;
        }

        if (role != "Director")
        {
            throw new ApprovalBlockedException("Only a Director may give the second approval.");
        }

        if (string.Equals(approver, invoice.FirstApprovedBy, StringComparison.OrdinalIgnoreCase))
        {
            throw new ApprovalBlockedException("A different user must give the second approval.");
        }

        invoice.Status = InvoiceStatus.Approved;
        invoice.DecidedBy = approver;
        invoice.DecidedAt = DateTime.UtcNow;
        _repository.Update(invoice);
        return invoice;
    }

    public Invoice Reject(string invoiceId, string approver, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required to reject an invoice.", nameof(reason));
        }

        var invoice = Find(invoiceId);
        EnsurePending(invoice);

        invoice.Status = InvoiceStatus.Rejected;
        invoice.DecidedBy = approver;
        invoice.DecidedAt = DateTime.UtcNow;
        invoice.DecisionNote = reason;
        _repository.Update(invoice);
        return invoice;
    }

    private Invoice Find(string invoiceId) =>
        _repository.GetById(invoiceId)
        ?? throw new KeyNotFoundException($"Invoice {invoiceId} was not found.");

    private static void EnsurePending(Invoice invoice)
    {
        if (invoice.Status != InvoiceStatus.PendingApproval)
        {
            throw new InvalidOperationException(
                $"Invoice {invoice.Id} is {invoice.Status}; only pending invoices can be decided.");
        }
    }
}
