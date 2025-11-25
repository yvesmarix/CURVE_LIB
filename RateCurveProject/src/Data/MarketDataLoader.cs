using ClosedXML.Excel;

namespace RateCurveProject.Data;

public enum InstrumentType
{
    Deposit,
    SWAP,
    BOND
}

public class MarketInstrument
{
    public InstrumentType Type { get; set; }
    public double MaturityYears { get; set; }
    public double Rate { get; set; }
    public int FixedFreq { get; set; }  // déterminé automatiquement
    public double Coupon { get; set; }  // lu depuis Excel (0 si absent)
}


public class MarketDataLoader
{
    public List<MarketInstrument> LoadInstruments(string xlsxPath)
    {
        var instruments = new List<MarketInstrument>();

        using var workbook = new XLWorkbook(xlsxPath);
        var ws = workbook.Worksheet(1);

        var usedRange = ws.RangeUsed();
        var headerRow = usedRange.FirstRow();

        // ░░░ Mapping dynamique header → colonne ░░░
        var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in headerRow.Cells())
            headerMap[cell.GetString().Trim()] = cell.Address.ColumnNumber;

        bool hasCoupon = headerMap.ContainsKey("Coupon");
        bool hasToBeSelected = headerMap.ContainsKey("DONOTSELECT");

        foreach (var row in usedRange.RowsUsed().Skip(1))
        {
            // ░░░ 1) FILTRE : ne garder que DONOTSELECT == "NON" ░░░
            if (hasToBeSelected)
            {
                string coef = row.Cell(headerMap["DONOTSELECT"]).GetString().Trim().ToUpperInvariant();
                if (coef != "NON")
                    continue;   // on ignore / passe cette ligne
            }

            // ░░░ 2) Lecture du coupon ░░░
            double couponVal = hasCoupon
                ? row.Cell(headerMap["Coupon"]).GetDouble()
                : 0.0;

            // ░░░ 3) Détection small-coupon → ZC ░░░
            bool treatAsZeroCoupon = couponVal < 0.5;

            double maturity = row.Cell(headerMap["Maturity"]).GetDouble();
            double pricePct = row.Cell(headerMap["Ask Price"]).GetDouble();

            // ░░░ 4) Calcul du taux actuariel ░░░
            double yield = BondYieldCalculator.ComputeYield(
                couponPct: treatAsZeroCoupon ? 0.0 : couponVal,
                maturityYears: maturity,
                pricePct: pricePct
            );

            // ░░░ 5) Construction de l’instrument ░░░
            var ins = new MarketInstrument
            {
                Type = InstrumentType.BOND,
                MaturityYears = maturity,

                Coupon = treatAsZeroCoupon ? 0.0 : couponVal,
                Rate = yield,

                FixedFreq = treatAsZeroCoupon ? 0 : 1
            };

            instruments.Add(ins);
        }

        // ░░░ 6) Nettoyage final ░░░
        return instruments
            .OrderBy(x => x.MaturityYears)
            .GroupBy(x => (x.Type, x.MaturityYears))
            .Select(g => g.Last())
            .ToList();
    }


    private InstrumentType ParseType(string s)
    {
        return Enum.Parse<InstrumentType>(s.Trim(), ignoreCase: true);
    }
}
