import { readFile } from 'node:fs/promises'

const pages = {
  'core-work/clients.md': 'clients',
  'core-work/gigs.md': 'gigs',
  'core-work/expenses-receipts-and-mileage.md': 'gigs',
  'core-work/create-review-and-send-invoices.md': 'invoices',
  'core-work/invoice-status-payments-and-income.md': 'invoices',
  'start-here/your-first-invoice.md': 'clients',
}

for (const [page, section] of Object.entries(pages)) {
  const contents = await readFile(new URL(`./src/content/docs/${page}`, import.meta.url), 'utf8')
  if (!contents.includes(`href="https://menu.glovelly.net/workspaces/${section}"`)) {
    throw new Error(`${page} must link to the ${section} workspace`)
  }
}
