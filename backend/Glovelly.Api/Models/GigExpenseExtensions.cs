namespace Glovelly.Api.Models;

public static class GigExpenseExtensions
{
    public static bool IsChargeableByDefault(this GigExpense expense)
    {
        return expense.ReimbursementStatus is not GigExpenseReimbursementStatus.Reimbursed
            and not GigExpenseReimbursementStatus.NotClaimable;
    }
}
