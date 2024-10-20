using Microsoft.SemanticKernel;
using System.ComponentModel;
using AITravelAgent;

class CurrencyConverter
{
    [KernelFunction, Description(@"Converts an amount from one currency to another
        and returns a friendly message with the results")]
    public static string ConvertAmount(
        [Description("The starting currency code")] string baseCurrencyCode,
        [Description("The target currency code")] string targetCurrencyCode, 
        [Description("The amount to convert")] string amount)
    {
        var currencyDictionary = Currency.Currencies;
        Currency targetCurrency = currencyDictionary[targetCurrencyCode];
        Currency baseCurrency = currencyDictionary[baseCurrencyCode];
        
        if (targetCurrency == null)
        {
            return targetCurrencyCode + " was not found";
        }
        else if (baseCurrency == null)
        {
            return baseCurrencyCode + " was not found";
        }
        else
        {
            double amountInUSD = Double.Parse(amount) * baseCurrency.USDPerUnit;
            double result = amountInUSD * targetCurrency.UnitsPerUSD;
            return $"${amount} {baseCurrencyCode} is approximately {result.ToString("C")} in {targetCurrency.Name}s ({targetCurrencyCode})";
        }
    }
}