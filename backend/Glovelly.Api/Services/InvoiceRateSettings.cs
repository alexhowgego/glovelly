namespace Glovelly.Api.Services;

public sealed class InvoiceRateSettings
{
    public const string SectionName = "InvoiceRates";

    public decimal DefaultMileageRate { get; set; } = 0.45m;
    public decimal DefaultPassengerMileageRate { get; set; } = 0.10m;
}
