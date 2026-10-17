import { expenseCategories } from '../gigEditing'
import type { ExpenseCategory } from '../gigEditing'

type Props = {
  amount: string; description: string; category: string; disabled: boolean; testIdPrefix?: string
  onAmount: (value: string) => void; onDescription: (value: string) => void; onCategory: (value: ExpenseCategory | '') => void
}
export function ExpenseFields({ amount, description, category, disabled, testIdPrefix, onAmount, onDescription, onCategory }: Props) {
  const id = (suffix: string) => testIdPrefix ? `${testIdPrefix}-${suffix}-input` : undefined
  return <div className="form-grid">
    <label><span>Amount</span><input data-testid={id('amount')} inputMode="decimal" value={amount} onChange={event => onAmount(event.target.value)} placeholder="0.00" disabled={disabled} /></label>
    <label><span>Description</span><input data-testid={id('description')} value={description} onChange={event => onDescription(event.target.value)} placeholder="Parking, hotel, equipment hire..." disabled={disabled} /></label>
    <label><span>Category</span><select data-testid={id('category')} value={category} onChange={event => onCategory(event.target.value as ExpenseCategory | '')} disabled={disabled}><option value="">No category</option>{expenseCategories.map(value => <option key={value}>{value}</option>)}</select></label>
  </div>
}
