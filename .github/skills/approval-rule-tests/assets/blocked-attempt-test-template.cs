[Fact]
public void Approve_Blocked_WhenRuleIsViolated_LeavesInvoiceUnchanged()
{
    var invoice = CreateInvoiceForBlockedAttempt();
    var before = Snapshot(invoice);
    var repository = new FakeInvoiceRepository(invoice);
    var service = CreateApprovalService(repository);

    var exception = Assert.Throws<ApprovalDeniedException>(
        () => service.Approve(/* blocked-attempt arguments */));

    Assert.Equal("Expected clear reason for this blocked rule", exception.Message);
    Assert.Equal(before, Snapshot(invoice));
    Assert.Equal(0, repository.UpdateCount);
}

private static InvoiceSnapshot Snapshot(Invoice invoice) => new(
    invoice.Id,
    invoice.CustomerName,
    invoice.Amount,
    invoice.Status,
    invoice.CreatedAt,
    invoice.DecidedBy,
    invoice.DecidedAt,
    invoice.DecisionNote);

private sealed record InvoiceSnapshot(
    string Id,
    string CustomerName,
    decimal Amount,
    InvoiceStatus Status,
    DateTime CreatedAt,
    string? DecidedBy,
    DateTime? DecidedAt,
    string? DecisionNote);

// Extend FakeInvoiceRepository to expose UpdateCount and increment it in Update.
// Replace the exception type and setup helpers with the project's actual test APIs.
