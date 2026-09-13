import { defineConfig } from 'astro/config'
import starlight from '@astrojs/starlight'

export default defineConfig({
  output: 'static',
  site: 'https://docs.glovelly.net',
  integrations: [
    starlight({
      title: 'Glovelly User Guide',
      description: 'Practical, step-by-step help for Glovelly.',
      social: [
        { icon: 'github', label: 'GitHub', href: 'https://github.com/alexhowgego/glovelly' },
      ],
      sidebar: [
        {
          label: 'Start here',
          items: [
            { label: 'What Glovelly is for', slug: 'start-here/what-glovelly-is-for' },
            { label: 'Sign in or request access', slug: 'start-here/sign-in-or-request-access' },
            { label: 'Your first invoice', slug: 'start-here/your-first-invoice' },
          ],
        },
        {
          label: 'Core work',
          items: [
            { label: 'Clients', slug: 'core-work/clients' },
            { label: 'Gigs', slug: 'core-work/gigs' },
            { label: 'Expenses, receipts, and mileage', slug: 'core-work/expenses-receipts-and-mileage' },
            { label: 'Create, review, and send invoices', slug: 'core-work/create-review-and-send-invoices' },
            { label: 'Invoice status, payments, and income', slug: 'core-work/invoice-status-payments-and-income' },
          ],
        },
        {
          label: 'Set up',
          items: [
            { label: 'Seller profile', slug: 'set-up/seller-profile' },
            { label: 'Settings and defaults', slug: 'set-up/settings-and-defaults' },
          ],
        },
        {
          label: 'Help',
          items: [
            { label: 'Common questions', slug: 'help/common-questions' },
            { label: 'Get help or report a problem', slug: 'help/get-help-or-report-a-problem' },
            { label: 'Privacy, terms, and technical help', slug: 'help/privacy-terms-and-technical-help' },
          ],
        },
      ],
      customCss: ['./src/styles/custom.css'],
      head: [
        {
          tag: 'link',
          attrs: { rel: 'icon', type: 'image/svg+xml', href: '/favicon.svg' },
        },
        {
          tag: 'meta',
          attrs: { name: 'theme-color', content: '#1d5575' },
        },
      ],
    }),
  ],
})
