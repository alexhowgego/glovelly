using System.Globalization;
using Glovelly.Api.Models;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class InvoiceMonetaryRoundingTests
{
    [Theory]
    [InlineData("118.9", "0.45", "53.51")]
    [InlineData("118.9", "-0.45", "-53.51")]
    [InlineData("1", "1.234", "1.23")]
    [InlineData("1", "1.236", "1.24")]
    [InlineData("1", "-1.234", "-1.23")]
    [InlineData("1", "-1.236", "-1.24")]
    [InlineData("2.5", "120.00", "300.00")]
    public void LineTotal_RoundsProductWithoutChangingOperands(string quantity, string unitPrice, string expected)
    {
        var line = new InvoiceLine
        {
            Quantity = decimal.Parse(quantity, CultureInfo.InvariantCulture),
            UnitPrice = decimal.Parse(unitPrice, CultureInfo.InvariantCulture),
        };

        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), line.LineTotal);
        Assert.Equal(decimal.Parse(quantity, CultureInfo.InvariantCulture), line.Quantity);
        Assert.Equal(decimal.Parse(unitPrice, CultureInfo.InvariantCulture), line.UnitPrice);
    }

    [Fact]
    public void Total_SumsRoundedLinesAndCancelsExactly()
    {
        var invoice = new Invoice();
        Assert.Equal(0m, invoice.Total);
        invoice.Lines.Add(new InvoiceLine { Quantity = 118.9m, UnitPrice = 0.45m });
        invoice.Lines.Add(new InvoiceLine { Quantity = 118.9m, UnitPrice = 0.45m });
        Assert.Equal(107.02m, invoice.Total);
        invoice.Lines.Add(new InvoiceLine { Quantity = 1m, UnitPrice = -invoice.Total });
        Assert.Equal(0m, invoice.Total);
    }
}
