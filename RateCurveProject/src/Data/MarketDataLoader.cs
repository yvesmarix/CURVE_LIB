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

        // Crée un mapping dynamique entre les noms des en-têtes et les numéros de colonne.
        var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in headerRow.Cells())
            headerMap[cell.GetString().Trim()] = cell.Address.ColumnNumber;

        bool hasCoupon = headerMap.ContainsKey("Coupon");
        bool hasToBeSelected = headerMap.ContainsKey("DONOTSELECT");

        foreach (var row in usedRange.RowsUsed().Skip(1))
        {
            // Filtrer les instruments non sélectionnés.
            // Si une colonne "DONOTSELECT" existe, on ignore les lignes qui ne contiennent pas "NON".
            if (hasToBeSelected)
            {
                string coef = row.Cell(headerMap["DONOTSELECT"]).GetString().Trim().ToUpperInvariant();
                if (coef != "NON")
                    continue;
            }

            // Lire la valeur du coupon.
            double couponVal = hasCoupon
                ? row.Cell(headerMap["Coupon"]).GetDouble()
                : 0.0;

            // Traiter les obligations à très faible coupon comme des zéro-coupons.
            bool treatAsZeroCoupon = couponVal < 0.5;

            double maturity = row.Cell(headerMap["Maturity"]).GetDouble();
            double pricePct = row.Cell(headerMap["Ask Price"]).GetDouble();

            // Calculer le rendement actuariel (yield) à partir du prix.
            double yield = BondYieldCalculator.ComputeYield(
                couponPct: treatAsZeroCoupon ? 0.0 : couponVal,
                maturityYears: maturity,
                pricePct: pricePct
            );

            // Créer l'objet MarketInstrument.
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

        // Nettoyage final de la liste.
        // On trie par maturité et on s'assure qu'il n'y a pas de doublons pour une même maturité.
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
