import { expect, test } from '@playwright/test';
import { InvoiceApprovalPage } from '../pages/invoice-approval.page';

test('Manager cannot approve an invoice above the approval limit', async ({ page }) => {
  const invoicePage = new InvoiceApprovalPage(page);
  const customerName = `Approval Limit ${new Date().toISOString()} ${Math.random()
    .toString(36)
    .slice(2, 8)}`;

  await page.goto('/');
  await invoicePage.signIn('demo.manager', 'ChangeMe123!');
  await invoicePage.createInvoice(customerName, '50000.01');

  const invoiceId = await invoicePage.invoiceIdForCustomer(customerName);
  await invoicePage.submitInvoice(invoiceId);
  await expect(invoicePage.invoiceStatusForCustomer(customerName)).toHaveText(
    'Pending Approval',
  );

  await invoicePage.openApprovals();
  await invoicePage.attemptApproval(invoiceId);
  await expect(invoicePage.decisionError()).toHaveText(
    'Manager may approve invoices up to 50,000.00.',
  );

  await page.reload();
  await expect(invoicePage.invoiceStatusForCustomer(customerName)).toHaveText(
    'Pending Approval',
  );
});
