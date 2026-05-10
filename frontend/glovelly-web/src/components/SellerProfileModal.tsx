import type { FormEvent } from 'react'
import type { SellerProfile, SellerProfileForm } from '../appShared'

type SellerProfileModalProps = {
  form: SellerProfileForm
  isOpen: boolean
  isSaving: boolean
  onClose: () => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
  onUpdateField: (field: keyof SellerProfileForm, value: string) => void
  profile: SellerProfile
  status: string
}

export function SellerProfileModal({
  form,
  isOpen,
  isSaving,
  onClose,
  onSubmit,
  onUpdateField,
  profile,
  status,
}: SellerProfileModalProps) {
  if (!isOpen) {
    return null
  }

  const liveMissingFields: string[] = []
  const normalizedRequiredFields = {
    sellerName: form.sellerName.trim(),
    addressLine1: form.addressLine1.trim(),
    city: form.city.trim(),
    country: form.country.trim(),
    accountName: form.accountName.trim(),
    sortCode: form.sortCode.trim(),
    accountNumber: form.accountNumber.trim(),
  }

  if (!normalizedRequiredFields.sellerName) {
    liveMissingFields.push('seller name')
  }

  if (!normalizedRequiredFields.addressLine1) {
    liveMissingFields.push('address line 1')
  }

  if (!normalizedRequiredFields.city) {
    liveMissingFields.push('city')
  }

  if (!normalizedRequiredFields.country) {
    liveMissingFields.push('country')
  }

  const hasAnyPaymentValue =
    Boolean(normalizedRequiredFields.accountName) ||
    Boolean(normalizedRequiredFields.sortCode) ||
    Boolean(normalizedRequiredFields.accountNumber)

  if (hasAnyPaymentValue) {
    if (!normalizedRequiredFields.accountName) {
      liveMissingFields.push('account name')
    }

    if (!normalizedRequiredFields.sortCode) {
      liveMissingFields.push('sort code')
    }

    if (!normalizedRequiredFields.accountNumber) {
      liveMissingFields.push('account number')
    }
  }

  const hasAnyLiveValue = Object.values(form).some((value) => value.trim().length > 0)

  const previewSellerRows = [
    form.sellerName,
    form.addressLine1,
    form.addressLine2,
    [form.city, form.region].filter(Boolean).join(', '),
    [form.postcode, form.country].filter(Boolean).join(', '),
    form.email ? `Email: ${form.email}` : '',
    form.phone ? `Phone: ${form.phone}` : '',
  ].filter(Boolean)

  const previewPaymentRows = [
    form.accountName ? `Account name: ${form.accountName}` : '',
    form.sortCode ? `Sort code: ${form.sortCode}` : '',
    form.accountNumber ? `Account number: ${form.accountNumber}` : '',
    form.paymentReferenceNote ? `Payment note: ${form.paymentReferenceNote}` : '',
  ].filter(Boolean)

  return (
    <div className="settings-overlay" onClick={onClose} role="presentation">
      <section
        aria-labelledby="seller-profile-title"
        aria-modal="true"
        className="settings-modal panel"
        onClick={(event) => event.stopPropagation()}
        role="dialog"
      >
        <div className="panel-heading">
          <div>
            <p className="section-label">Settings</p>
            <h2 id="seller-profile-title">Seller profile</h2>
          </div>
          <button className="ghost-button" onClick={onClose} type="button">
            Close
          </button>
        </div>

        <p className="hero-text settings-intro">
          These are the sender and payment details Glovelly uses on generated invoices.
          They are customer-visible, so optimise for accuracy and clarity.
        </p>

        {!hasAnyLiveValue && !profile.isConfigured && (
          <div className="settings-note seller-profile-empty">
            Add your sender identity and remittance details here so invoice PDFs have a
            reliable source of truth.
          </div>
        )}

        <form className="settings-form" onSubmit={onSubmit}>
          <div className="seller-profile-layout">
            <div className="seller-profile-sections">
              <section className="seller-profile-section">
                <div>
                  <p className="section-label">Seller identity</p>
                  <h3>Shown in the invoice header</h3>
                </div>

                <div className="form-grid">
                  <label className="full-width">
                    <span>Seller / trading name</span>
                    <input
                      value={form.sellerName}
                      onChange={(event) => onUpdateField('sellerName', event.target.value)}
                      placeholder="Glovelly Music Ltd"
                    />
                  </label>

                  <label className="full-width">
                    <span>Address line 1</span>
                    <input
                      value={form.addressLine1}
                      onChange={(event) => onUpdateField('addressLine1', event.target.value)}
                      placeholder="1 Chapel Street"
                    />
                  </label>

                  <label className="full-width">
                    <span>Address line 2</span>
                    <input
                      value={form.addressLine2}
                      onChange={(event) => onUpdateField('addressLine2', event.target.value)}
                      placeholder="Optional"
                    />
                  </label>

                  <label>
                    <span>Town / city</span>
                    <input
                      value={form.city}
                      onChange={(event) => onUpdateField('city', event.target.value)}
                      placeholder="Manchester"
                    />
                  </label>

                  <label>
                    <span>County / region</span>
                    <input
                      value={form.region}
                      onChange={(event) => onUpdateField('region', event.target.value)}
                      placeholder="Greater Manchester"
                    />
                  </label>

                  <label>
                    <span>Postcode</span>
                    <input
                      value={form.postcode}
                      onChange={(event) => onUpdateField('postcode', event.target.value)}
                      placeholder="M1 1AA"
                    />
                  </label>

                  <label>
                    <span>Country</span>
                    <input
                      value={form.country}
                      onChange={(event) => onUpdateField('country', event.target.value)}
                      placeholder="United Kingdom"
                    />
                  </label>

                  <label>
                    <span>Contact email</span>
                    <input
                      type="email"
                      value={form.email}
                      onChange={(event) => onUpdateField('email', event.target.value)}
                      placeholder="accounts@example.com"
                    />
                  </label>

                  <label>
                    <span>Contact phone</span>
                    <input
                      value={form.phone}
                      onChange={(event) => onUpdateField('phone', event.target.value)}
                      placeholder="+44 7700 900123"
                    />
                  </label>
                </div>
              </section>

              <section className="seller-profile-section">
                <div>
                  <p className="section-label">Payment details</p>
                  <h3>Shown in the remittance section</h3>
                </div>

                <div className="form-grid">
                  <label>
                    <span>Account name</span>
                    <input
                      value={form.accountName}
                      onChange={(event) => onUpdateField('accountName', event.target.value)}
                      placeholder="Glovelly Music Ltd"
                    />
                  </label>

                  <label>
                    <span>Sort code</span>
                    <input
                      value={form.sortCode}
                      onChange={(event) => onUpdateField('sortCode', event.target.value)}
                      placeholder="12-34-56"
                    />
                  </label>

                  <label>
                    <span>Account number</span>
                    <input
                      value={form.accountNumber}
                      onChange={(event) => onUpdateField('accountNumber', event.target.value)}
                      placeholder="12345678"
                    />
                  </label>

                  <label className="full-width">
                    <span>Payment reference note</span>
                    <textarea
                      rows={3}
                      value={form.paymentReferenceNote}
                      onChange={(event) =>
                        onUpdateField('paymentReferenceNote', event.target.value)
                      }
                      placeholder="Use the invoice number as the payment reference."
                    />
                  </label>
                </div>

                <div className="settings-note">
                  Payment details are visible to your customer on generated invoices.
                </div>
              </section>
            </div>

            <aside className="seller-profile-preview">
              <div>
                <p className="section-label">Preview</p>
                <h3>Invoice identity</h3>
              </div>
              <p className="seller-profile-preview-note">
                This is how your details will appear on generated invoices.
              </p>

              <div className="seller-preview-card">
                <strong>Seller block</strong>
                {previewSellerRows.length > 0 ? (
                  previewSellerRows.map((row) => <span key={row}>{row}</span>)
                ) : (
                  <span>Add seller details to see the preview take shape.</span>
                )}
              </div>

              <div className="seller-preview-card">
                <strong>Payment details</strong>
                {previewPaymentRows.length > 0 ? (
                  previewPaymentRows.map((row) => <span key={row}>{row}</span>)
                ) : (
                  <span>Add bank and remittance details to preview the payment section.</span>
                )}
              </div>

              {liveMissingFields.length > 0 && (
                <div className="settings-note">
                  Invoice-ready fields still missing: {liveMissingFields.join(', ')}.
                </div>
              )}
            </aside>
          </div>

          <div className="form-actions">
            <button className="primary-button" type="submit" disabled={isSaving}>
              {isSaving ? 'Saving…' : 'Save seller profile'}
            </button>
            <span className="status-pill">{status}</span>
          </div>
        </form>
      </section>
    </div>
  )
}
