using System.Globalization;
using System.Text.Json;
using InvoiceApp.Api.Auth;
using InvoiceApp.Api.Controllers;
using InvoiceApp.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace InvoiceApp.Tests;

public class ApprovalsControllerTests
{
    private static Invoice InvoiceFor(decimal amount) =>
        new()
        {
            Id = "INV-API",
            CustomerName = "API Test School",
            Amount = amount,
            Status = InvoiceStatus.PendingApproval,
            CreatedAt = new DateTime(2025, 5, 10, 8, 0, 0, DateTimeKind.Utc),
            DecidedBy = "prior.approver",
            DecidedAt = new DateTime(2025, 5, 11, 9, 0, 0, DateTimeKind.Utc),
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

    private static (ApprovalsController Controller, FakeInvoiceRepository Repository) CreateController(
        Invoice invoice,
        string username,
        string role)
    {
        var repository = new FakeInvoiceRepository(invoice);
        var controller = new ApprovalsController(new ApprovalService(repository));
        var httpContext = new DefaultHttpContext();
        httpContext.Items[HttpContextExtensions.SessionKey] = new UserSession(username, role);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return (controller, repository);
    }

    private static ActionExecutingContext CreateRejectActionContext(ApprovalsController controller)
    {
        var actionContext = new ActionContext(
            controller.HttpContext,
            new RouteData(),
            new ActionDescriptor());
        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller);
    }

    private static RequireRoleAttribute RejectRoleFilter() =>
        (RequireRoleAttribute)typeof(ApprovalsController)
            .GetMethod(nameof(ApprovalsController.Reject))!
            .GetCustomAttributes(typeof(RequireRoleAttribute), inherit: true)
            .Single();

    private sealed record InvoiceSnapshot(
        string Id,
        string CustomerName,
        decimal Amount,
        InvoiceStatus Status,
        DateTime CreatedAt,
        string? DecidedBy,
        DateTime? DecidedAt,
        string? DecisionNote);

    [Theory]
    [InlineData("Manager", "50000.01", "Manager may approve invoices up to 50,000.00.")]
    [InlineData("Manager", "500000.01", "Invoices above 500,000.00 require two approvers.")]
    [InlineData("Director", "500000.01", "Invoices above 500,000.00 require two approvers.")]
    [InlineData("Admin", "500000.01", "Only Manager or Director may approve invoices.")]
    [InlineData("Clerk", "500000.01", "Only Manager or Director may approve invoices.")]
    public void Approve_MapsBlockedApprovalTo403WithExactErrorBodyAndNoMutation(
        string role,
        string amountText,
        string expectedReason)
    {
        var amount = decimal.Parse(amountText, CultureInfo.InvariantCulture);
        var invoice = InvoiceFor(amount);
        var before = Snapshot(invoice);
        var (controller, repository) = CreateController(invoice, $"demo.{role.ToLowerInvariant()}", role);

        var action = controller.Approve(invoice.Id);

        var response = Assert.IsType<ObjectResult>(action.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        using var body = JsonDocument.Parse(JsonSerializer.Serialize(response.Value));
        Assert.Equal(expectedReason, body.RootElement.GetProperty("error").GetString());
        Assert.Single(body.RootElement.EnumerateObject());
        Assert.Equal(before, Snapshot(invoice));
        Assert.Equal(0, repository.UpdateCount);
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("Director")]
    [InlineData("Admin")]
    public void Reject_AllowsManagerDirectorAndAdminWithRequiredReasonAndNoAmountLimit(string role)
    {
        var invoice = InvoiceFor(1_000_000.00m);
        var (controller, repository) = CreateController(invoice, $"demo.{role.ToLowerInvariant()}", role);
        var filterContext = CreateRejectActionContext(controller);

        RejectRoleFilter().OnActionExecuting(filterContext);

        Assert.Null(filterContext.Result);
        var action = controller.Reject(invoice.Id, new ApprovalsController.RejectRequest("Reviewed and rejected"));

        var response = Assert.IsType<OkObjectResult>(action.Result);
        var result = Assert.IsType<Invoice>(response.Value);
        Assert.Equal(InvoiceStatus.Rejected, result.Status);
        Assert.Equal("Reviewed and rejected", result.DecisionNote);
        Assert.Equal($"demo.{role.ToLowerInvariant()}", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
        Assert.Equal(1, repository.UpdateCount);
    }

    [Fact]
    public void Reject_ForbidsOtherRolesWithoutChangingInvoice()
    {
        var invoice = InvoiceFor(1_000_000.00m);
        var before = Snapshot(invoice);
        var (controller, repository) = CreateController(invoice, "demo.clerk", "Clerk");
        var filterContext = CreateRejectActionContext(controller);

        RejectRoleFilter().OnActionExecuting(filterContext);

        Assert.IsType<StatusCodeResult>(filterContext.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, ((StatusCodeResult)filterContext.Result!).StatusCode);
        Assert.Equal(before, Snapshot(invoice));
        Assert.Equal(0, repository.UpdateCount);
    }

    [Fact]
    public void Reject_RequiresAReasonAtTheApiBoundary()
    {
        var invoice = InvoiceFor(100m);
        var before = Snapshot(invoice);
        var (controller, repository) = CreateController(invoice, "demo.manager", "Manager");

        var action = controller.Reject(invoice.Id, new ApprovalsController.RejectRequest("  "));

        var response = Assert.IsType<BadRequestObjectResult>(action.Result);
        using var body = JsonDocument.Parse(JsonSerializer.Serialize(response.Value));
        Assert.Equal(
            "A reason is required to reject an invoice. (Parameter 'reason')",
            body.RootElement.GetProperty("error").GetString());
        Assert.Equal(before, Snapshot(invoice));
        Assert.Equal(0, repository.UpdateCount);
    }
}
