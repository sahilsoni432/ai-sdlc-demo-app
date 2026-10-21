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
            DecisionNote = "Prior decision note"
        };

    private static Invoice InvoiceWithFirstApproval(decimal amount, string approver) =>
        new()
        {
            Id = "INV-LIMIT",
            CustomerName = "Test School",
            Amount = amount,
            Status = InvoiceStatus.PendingApproval,
            CreatedAt = new DateTime(2025, 4, 12, 9, 30, 0, DateTimeKind.Utc),
            FirstApprovedBy = approver,
            FirstApprovedAt = new DateTime(2025, 4, 13, 10, 15, 0, DateTimeKind.Utc),
            DecisionNote = "Prior decision note"
        };

    private static InvoiceSnapshot Snapshot(Invoice invoice) => new(
        invoice.Id,
        invoice.CustomerName,
        invoice.Amount,
        invoice.Status,
        invoice.CreatedAt,
        invoice.FirstApprovedBy,
        invoice.FirstApprovedAt,
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
        string? FirstApprovedBy,
        DateTime? FirstApprovedAt,
        string? DecidedBy,
        DateTime? DecidedAt,
        string? DecisionNote);

    [Fact]
    public void Submit_MovesDraftToPendingApproval()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.Draft);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);

        var result = service.Submit("INV-1");

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
        Assert.Equal(1, repository.UpdateCount);
    }

    [Fact]
    public void Submit_RejectsInvoiceThatIsNotADraft()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.Approved);
        var before = Snapshot(invoice);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);

        var exception = Assert.Throws<InvalidOperationException>(() => service.Submit("INV-1"));

        Assert.Equal("Invoice INV-1 is Approved; only drafts can be submitted.", exception.Message);
        Assert.Equal(before, Snapshot(invoice));
        Assert.Equal(0, repository.UpdateCount);
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
    [InlineData("Manager", "demo.manager")]
    [InlineData("Director", "demo.director")]
    public void Approve_RecordsFirstApprovalAbove500000WithoutDecidingInvoice(string role, string approver)
    {
        var invoice = InvoiceForApproval(500_000.01m);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);
        var approvalStartedAt = DateTime.UtcNow;

        var result = service.Approve(invoice.Id, approver, role);

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
        Assert.Equal(approver, result.FirstApprovedBy);
        Assert.NotNull(result.FirstApprovedAt);
        Assert.InRange(result.FirstApprovedAt.Value, approvalStartedAt, DateTime.UtcNow);
        Assert.Null(result.DecidedBy);
        Assert.Null(result.DecidedAt);
        Assert.Equal(1, repository.UpdateCount);
    }

    [Theory]
    [InlineData("demo.manager", "demo.director")]
    [InlineData("demo.director", "demo.director2")]
    public void Approve_DifferentDirectorCompletesApprovalAbove500000(
        string firstApprover,
        string secondApprover)
    {
        var invoice = InvoiceWithFirstApproval(500_000.01m, firstApprover);
        var firstApprovalAt = invoice.FirstApprovedAt;
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);

        var result = service.Approve(invoice.Id, secondApprover, "Director");

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal(firstApprover, result.FirstApprovedBy);
        Assert.Equal(firstApprovalAt, result.FirstApprovedAt);
        Assert.Equal(secondApprover, result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
        Assert.Equal(1, repository.UpdateCount);
    }

    [Fact]
    public void Approve_BlocksManagerAsSecondApproverWithoutChangingFirstApproval()
    {
        var invoice = InvoiceWithFirstApproval(500_000.01m, "demo.director");
        var before = Snapshot(invoice);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);

        var exception = Assert.Throws<ApprovalBlockedException>(
            () => service.Approve(invoice.Id, "demo.manager2", "Manager"));

        AssertBlockedAttemptUnchanged(
            invoice,
            before,
            repository,
            exception,
            "Only a Director may give the second approval.");
    }

    [Fact]
    public void Approve_ManagerFirstThenSameManagerRetryIsBlockedByDirectorOnlyRule()
    {
        var invoice = InvoiceForApproval(500_000.01m);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);
        service.Approve(invoice.Id, "demo.manager", "Manager");
        var beforeRetry = Snapshot(invoice);

        var exception = Assert.Throws<ApprovalBlockedException>(
            () => service.Approve(invoice.Id, "demo.manager", "Manager"));

        Assert.Equal("Only a Director may give the second approval.", exception.Message);
        Assert.Equal(beforeRetry, Snapshot(invoice));
        Assert.Equal(1, repository.UpdateCount);
    }

    [Theory]
    [InlineData("demo.manager")]
    [InlineData("DEMO.MANAGER")]
    public void Approve_BlocksSameUserAsSecondApproverAndDifferentDirectorCanComplete(string retryUsername)
    {
        var invoice = InvoiceWithFirstApproval(500_000.01m, "demo.manager");
        var firstApprovalAt = invoice.FirstApprovedAt;
        var beforeAttempt = Snapshot(invoice);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);

        var exception = Assert.Throws<ApprovalBlockedException>(
            () => service.Approve(invoice.Id, retryUsername, "Director"));

        AssertBlockedAttemptUnchanged(
            invoice,
            beforeAttempt,
            repository,
            exception,
            "A different user must give the second approval.");
        Assert.Equal("demo.manager", invoice.FirstApprovedBy);
        Assert.Equal(firstApprovalAt, invoice.FirstApprovedAt);

        var result = service.Approve(invoice.Id, "demo.director", "Director");

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.manager", result.FirstApprovedBy);
        Assert.Equal(firstApprovalAt, result.FirstApprovedAt);
        Assert.Equal("demo.director", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
        Assert.Equal(1, repository.UpdateCount);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Clerk")]
    public void Approve_IneligibleRoleTakesPrecedenceAbove500000AndLeavesInvoiceUnchanged(string role)
    {
        var invoice = InvoiceWithFirstApproval(500_000.01m, "demo.manager");
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
        var invoice = InvoiceWith("INV-1", InvoiceStatus.Draft);
        var before = Snapshot(invoice);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);

        var exception = Assert.Throws<InvalidOperationException>(
            () => service.Approve("INV-1", "demo.manager", "Manager"));

        Assert.Equal("Invoice INV-1 is Draft; only pending invoices can be decided.", exception.Message);
        Assert.Equal(before, Snapshot(invoice));
        Assert.Equal(0, repository.UpdateCount);
    }

    [Fact]
    public void Approve_ThrowsWhenInvoiceDoesNotExist()
    {
        var repository = new FakeInvoiceRepository();
        var service = new ApprovalService(repository);

        var exception = Assert.Throws<KeyNotFoundException>(
            () => service.Approve("INV-404", "demo.manager", "Manager"));

        Assert.Equal("Invoice INV-404 was not found.", exception.Message);
        Assert.Equal(0, repository.UpdateCount);
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

    [Fact]
    public void Reject_RejectsInvoiceThatIsNotPendingWithoutChangingInvoice()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.Rejected);
        var before = Snapshot(invoice);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);

        var exception = Assert.Throws<InvalidOperationException>(
            () => service.Reject("INV-1", "demo.manager", "Duplicate invoice"));

        Assert.Equal("Invoice INV-1 is Rejected; only pending invoices can be decided.", exception.Message);
        Assert.Equal(before, Snapshot(invoice));
        Assert.Equal(0, repository.UpdateCount);
    }
}
