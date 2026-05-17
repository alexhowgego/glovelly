namespace Glovelly.Api.Services;

public interface IInvoiceNumberService
{
    Task<string> GenerateInvoiceNumberAsync(DateOnly invoiceDate, CancellationToken cancellationToken = default);
}
