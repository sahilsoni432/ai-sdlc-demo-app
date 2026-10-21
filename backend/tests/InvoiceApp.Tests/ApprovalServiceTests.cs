using InvoiceApp.Domain;
using Xunit;

namespace InvoiceApp.Tests;

public class ApprovalServiceTests
{
    private static Invoice InvoiceWith(string id, InvoiceStatus status) =>
        new() { Id = id, CustomerName = "Test School", Amount = 100m, Status = status };

    private static ApprovalService ServiceFor(params Invoice[] invoices) =>
        new(new FakeInvoiceRepository(invoices));

    private static Invoice InvoiceForApproval(decimal amount) =>
        new()
        {
            Id = "INV-LIMIT",
            CustomerName = "Test School",
            Amount = amount,
            Status = InvoiceStatus.PendingApproval,
            CreatedAt = new DateTime(2025, 4, 12, 9, 30, 0, DateTimeKind.Utc),
            DecidedBy = "prior.approver",
            DecidedAt = new DateTime(2025, 4, 13, 10, 15, 0, DateTimeKind.Utc),
            DecisionNote = "Prior decision note"
        };

    private static InvoiceSnapshot Snapshot(Invoice invoice) => new(
        invoice.Id,
        invoice.CustomerName,
        invoice.Amount,
        invoice.Status,
        invoice.CreatedAt,
        invoice.DecidedBy,
        invoice.DecidedAt,
        invoice.DecisionNote);

    private static void AssertBlockedAttemptUnchanged(
        Invoice invoice,
        InvoiceSnapshot before,
        FakeInvoiceRepository repository,
        ApprovalBlockedException exception,
        string expectedReason)
    {
        Assert.Equal(expectedReason, exception.Message);
        Assert.Equal(before, Snapshot(invoice));
        Assert.Equal(0, repository.UpdateCount);
    }

    private sealed record InvoiceSnapshot(
        string Id,
        string CustomerName,
        decimal Amount,
        InvoiceStatus Status,
        DateTime CreatedAt,
        string? DecidedBy,
        DateTime? DecidedAt,
        string? DecisionNote);

    [Fact]
    public void Submit_MovesDraftToPendingApproval()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.Draft);

        var result = ServiceFor(invoice).Submit("INV-1");

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
    }

    [Fact]
    public void Submit_RejectsInvoiceThatIsNotADraft()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.Approved));

        Assert.Throws<InvalidOperationException>(() => service.Submit("INV-1"));
    }

    [Fact]
    public void Approve_SetsStatusApproverAndTimestamp()
    {
        var repository = new FakeInvoiceRepository(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));
        var service = new ApprovalService(repository);

        var result = service.Approve("INV-1", "demo.manager", "Manager");

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.manager", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
        Assert.Equal(1, repository.UpdateCount);
    }

    [Fact]
    public void Approve_ManagerMayApproveAtExactly50000()
    {
        var invoice = InvoiceForApproval(50_000.00m);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);

        var result = service.Approve(invoice.Id, "demo.manager", "Manager");

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.manager", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
        Assert.Equal(1, repository.UpdateCount);
    }

    [Fact]
    public void Approve_BlocksManagerAt50000Point01WithoutChangingInvoice()
    {
        var invoice = InvoiceForApproval(50_000.01m);
        var before = Snapshot(invoice);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);

        var exception = Assert.Throws<ApprovalBlockedException>(
            () => service.Approve(invoice.Id, "demo.manager", "Manager"));

        AssertBlockedAttemptUnchanged(
            invoice,
            before,
            repository,
            exception,
            "Manager may approve invoices up to 50,000.00.");
    }

    [Fact]
    public void Approve_BlocksManagerAtExactly500000WithoutChangingInvoice()
    {
        var invoice = InvoiceForApproval(500_000.00m);
        var before = Snapshot(invoice);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);

        var exception = Assert.Throws<ApprovalBlockedException>(
            () => service.Approve(invoice.Id, "demo.manager", "Manager"));

        AssertBlockedAttemptUnchanged(
            invoice,
            before,
            repository,
            exception,
            "Manager may approve invoices up to 50,000.00.");
    }

    [Fact]
    public void Approve_DirectorMayApproveAtExactly500000()
    {
        var invoice = InvoiceForApproval(500_000.00m);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);

        var result = service.Approve(invoice.Id, "demo.director", "Director");

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.director", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
        Assert.Equal(1, repository.UpdateCount);
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("Director")]
    public void Approve_BlocksEligibleRoleAbove500000WithoutChangingInvoice(string role)
    {
        var invoice = InvoiceForApproval(500_000.01m);
        var before = Snapshot(invoice);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);

        var exception = Assert.Throws<ApprovalBlockedException>(
            () => service.Approve(invoice.Id, $"demo.{role.ToLowerInvariant()}", role));

        AssertBlockedAttemptUnchanged(
            invoice,
            before,
            repository,
            exception,
            "Invoices above 500,000.00 require two approvers.");
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Clerk")]
    public void Approve_IneligibleRoleTakesPrecedenceAbove500000AndLeavesInvoiceUnchanged(string role)
    {
        var invoice = InvoiceForApproval(500_000.01m);
        var before = Snapshot(invoice);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);

        var exception = Assert.Throws<ApprovalBlockedException>(
            () => service.Approve(invoice.Id, $"demo.{role.ToLowerInvariant()}", role));

        AssertBlockedAttemptUnchanged(
            invoice,
            before,
            repository,
            exception,
            "Only Manager or Director may approve invoices.");
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Clerk")]
    public void Approve_BlocksIneligibleRoleAtNormalAmountWithoutChangingInvoice(string role)
    {
        var invoice = InvoiceForApproval(100.00m);
        var before = Snapshot(invoice);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);

        var exception = Assert.Throws<ApprovalBlockedException>(
            () => service.Approve(invoice.Id, $"demo.{role.ToLowerInvariant()}", role));

        AssertBlockedAttemptUnchanged(
            invoice,
            before,
            repository,
            exception,
            "Only Manager or Director may approve invoices.");
    }

    [Fact]
    public void Approve_RejectsInvoiceThatIsStillADraft()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.Draft));

        Assert.Throws<InvalidOperationException>(() => service.Approve("INV-1", "demo.manager", "Manager"));
    }

    [Fact]
    public void Approve_ThrowsWhenInvoiceDoesNotExist()
    {
        var service = ServiceFor();

        Assert.Throws<KeyNotFoundException>(() => service.Approve("INV-404", "demo.manager", "Manager"));
    }

    [Fact]
    public void Reject_RecordsReasonAndApprover()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));

        var result = service.Reject("INV-1", "demo.manager", "Duplicate invoice");

        Assert.Equal(InvoiceStatus.Rejected, result.Status);
        Assert.Equal("Duplicate invoice", result.DecisionNote);
    }

    [Fact]
    public void Reject_RequiresAReason()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));

        Assert.Throws<ArgumentException>(() => service.Reject("INV-1", "demo.manager", "  "));
    }
}
