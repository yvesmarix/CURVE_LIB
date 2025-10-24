using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace RateCurveProject.Data;

public enum InstrumentType
{
    Deposit,
    Swap,
    OAT
}

public class MarketInstrument
{
    public InstrumentType Type { get; set; }
    public double MaturityYears { get; set; }
    public double Rate { get; set; }
    public string DayCount { get; set; }
    public int FixedFreq { get; set; } // Fréquence des paiements fixes (ex: 1 pour annuel, 2 pour semi-annuel)
}

public class MarketDataLoader
{
    public List<MarketInstrument> LoadInstruments(string csvPath)
    {
        // Configuration CSV pour gérer les délimiteurs `;` et les valeurs avec `,`
        var config = new CsvConfiguration(new CultureInfo("fr-FR")) // Utilise fr-FR pour gérer les virgules
        {
            Delimiter = ";", // Délimiteur `;`
            TrimOptions = TrimOptions.Trim,
            IgnoreBlankLines = true,
            PrepareHeaderForMatch = args => args.Header.Trim().ToLowerInvariant(),
        };

        using var reader = new StreamReader(csvPath);
        using var csv = new CsvReader(reader, config);

        // Lecture directe des instruments
        var instruments = csv.GetRecords<MarketInstrument>().ToList();

        // Tri et suppression des doublons
        return instruments
            .OrderBy(x => x.MaturityYears)
            .GroupBy(x => (x.Type, x.MaturityYears))
            .Select(g => g.Last())
            .ToList();
    }
}