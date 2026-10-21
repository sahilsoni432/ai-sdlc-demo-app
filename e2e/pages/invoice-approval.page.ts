import { expect, type Locator, type Page } from '@playwright/test';

export class InvoiceApprovalPage {
  constructor(private readonly page: Page) {}

  async signIn(username: string, password: string): Promise<void> {
    await this.page.getByLabel('Username').fill(username);
    await this.page.getByLabel('Password').fill(password);
    await this.page.getByRole('button', { name: 'Sign in' }).click();
  }

  async createInvoice(customerName: string, amount: string): Promise<void> {
    await this.page.getByLabel('Customer name').fill(customerName);
    await this.page.getByLabel('Amount').fill(amount);
    await this.page.getByRole('button', { name: 'Create invoice' }).click();
    await expect(this.invoiceRow(customerName)).toHaveCount(1);
  }

  async invoiceIdForCustomer(customerName: string): Promise<string> {
    const rowText = await this.invoiceRow(customerName).innerText();
    const invoiceId = rowText.match(/\bINV-\d+\b/)?.[0];

    if (!invoiceId) {
      throw new Error(`Could not find the invoice ID for customer "${customerName}".`);
    }

    return invoiceId;
  }

  async submitInvoice(invoiceId: string): Promise<void> {
    await this.page
      .getByRole('button', { name: `Submit invoice ${invoiceId} for approval` })
      .click();
  }

  async openApprovals(): Promise<void> {
    await this.page.getByRole('tab', { name: 'Approvals' }).click();
  }

  async attemptApproval(invoiceId: string): Promise<void> {
    await this.page.getByRole('button', { name: `Approve invoice ${invoiceId}` }).click();
  }

  invoiceStatusForCustomer(customerName: string): Locator {
    return this.invoiceRow(customerName).getByRole('cell', {
      name: 'Pending Approval',
      exact: true,
    });
  }

  decisionError(): Locator {
    return this.page.getByRole('alert');
  }

  private invoiceRow(customerName: string): Locator {
    return this.page.getByRole('row').filter({ hasText: customerName });
  }
}
