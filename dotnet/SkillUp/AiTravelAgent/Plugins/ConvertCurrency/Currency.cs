namespace AITravelAgent;

public class Currency
{
    public required string Name { get; set; }
    public double UnitsPerUSD { get; set; }
    public double USDPerUnit { get; set; }

    // Use a static constructor to ensure dictionary initialization
    static Currency()
    {
        currencyDictionary = [];
    }

    private static Dictionary<string, Currency> currencyDictionary;

    public static Dictionary<string, Currency> Currencies
    {
        get
        {
            if (currencyDictionary.Count == 0)
            {
                InitializeCurrencies();
            }

            return currencyDictionary;
        }
    }

    public static void InitializeCurrencies()
    {
        currencyDictionary = [];

        string filePath = Path.Combine(
            Directory.GetCurrentDirectory(), 
            "Plugins//ConvertCurrency//currencies.txt"
        );
        
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"Couldn't find currencies.txt at {filePath}"
            );
        }

        using StreamReader reader = new(filePath);

        // Skip the header row
        reader.ReadLine();

        while (!reader.EndOfStream)
        {
            string line = reader.ReadLine()!;
            string[] columns = line.Split('\t');

            if (columns.Length == 4)
            {
                // Create a Currency object
                Currency currency = new()
                {
                    Name = columns[1],
                    UnitsPerUSD = double.Parse(columns[2]),
                    USDPerUnit = double.Parse(columns[3])
                };

                // Add the currency to the dictionary
                currencyDictionary.Add(columns[0], currency);
            }
        }
    }
}