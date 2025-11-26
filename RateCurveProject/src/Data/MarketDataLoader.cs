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

    /// <summary>
    /// Taux de marché :
    /// - Dépôts : taux simple
    /// - Swaps  : swap rate
    /// - Bonds  : yield (rendement actuariel) utilisé à des fins d'affichage/diagnostic
    /// </summary>
    public double Rate { get; set; }

    /// <summary>
    /// Fréquence de la jambe fixe (par an) pour swap / obligation à coupon.
    /// Pour les zéro-coupons, on met typiquement 0.
    /// </summary>
    public int FixedFreq { get; set; }

    /// <summary>
    /// Coupon nominal en POURCENT (ex : 4.5 signifie 4.5% par an).
    /// </summary>
    public double Coupon { get; set; }

    /// <summary>
    /// Prix de marché en fraction du nominal (ex : 99.50% → 0.9950).
    /// </summary>
    public double Price { get; set; }
}

public class MarketDataLoader
{
    public List<MarketInstrument> LoadInstruments(string xlsxPath)
    {
        var instruments = new List<MarketInstrument>();

        using var workbook = new XLWorkbook(xlsxPath);
        var ws = workbook.Worksheet(1);

        var usedRange = ws.RangeUsed();
        if (usedRange is null)
            return new List<MarketInstrument>();

        var headerRow = usedRange.FirstRow();

        // Mapping dynamique des colonnes
        var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in headerRow.Cells())
        {
            var header = cell.GetString().Trim();
            if (!string.IsNullOrEmpty(header))
                headerMap[header] = cell.Address.ColumnNumber;
        }

        bool hasCoupon = headerMap.ContainsKey("Coupon");
        bool hasToBeSelected = headerMap.ContainsKey("DONOTSELECT");

        foreach (var row in usedRange.RowsUsed().Skip(1))
        {
            // Filtrer les instruments non sélectionnés (si la colonne existe)
            if (hasToBeSelected)
            {
                string coef = row.Cell(headerMap["DONOTSELECT"]).GetString().Trim().ToUpperInvariant();
                if (coef != "NON")
                    continue;
            }

            // Coupon lu dans le fichier (en %)
            double couponVal = hasCoupon
                ? row.Cell(headerMap["Coupon"]).GetDouble()
                : 0.0;

            // Traiter les très faibles coupons comme des zéro-coupons
            bool treatAsZeroCoupon = couponVal < 0.5;

            double maturity = row.Cell(headerMap["Maturity"]).GetDouble();

            // Prix coté (souvent en % du nominal)
            double pricePct = row.Cell(headerMap["Ask Price"]).GetDouble();
            double price = pricePct / 100.0; // Converti en fraction du nominal

            // Calcul du yield à partir du prix (optionnel mais utile pour affichage)
            double yield = BondYieldCalculator.ComputeYield(
                couponPct: treatAsZeroCoupon ? 0.0 : couponVal,
                maturityYears: maturity,
                pricePct: pricePct
            );

            var ins = new MarketInstrument
            {
                Type = InstrumentType.BOND,
                MaturityYears = maturity,
                Coupon = treatAsZeroCoupon ? 0.0 : couponVal,
                Rate = yield,
                FixedFreq = treatAsZeroCoupon ? 0 : 1,  // 1 = annuel, 0 pour zéro-coupon
                Price = price
            };

            instruments.Add(ins);
        }

        // Tri + suppression de doublons (même type & maturité)
        return instruments
            .OrderBy(x => x.MaturityYears)
            .GroupBy(x => (x.Type, x.MaturityYears))
            .Select(g => g.Last())
            .ToList();
    }
}
