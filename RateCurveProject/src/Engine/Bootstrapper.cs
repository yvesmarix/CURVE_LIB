using RateCurveProject.Data;
using RateCurveProject.Models;

namespace RateCurveProject.Engine;

/// <summary>
/// Bootstrap d'une courbe zéro-coupon à partir de :
/// - dépôts
/// - swaps vanilla
/// - obligations (OAT) à coupon
/// 
/// Hypothèses simplifiées :
/// - Coupons des BOND payés à fréquence FixedFreq (souvent 1 pour annuel).
/// - Nominal = 1.
/// - MarketInstrument.Price = prix de marché (fraction du nominal).
/// - MarketInstrument.Rate = yield (pour BOND), taux du dépôt, taux du swap.
/// </summary>
public class Bootstrapper
{
    public List<CurvePoint> BuildZeroCurve(List<MarketInstrument> instruments)
    {
        instruments = instruments.OrderBy(i => i.MaturityYears).ToList();

        var points = new List<CurvePoint>();

        // Interpolation de DF(t) à partir des points déjà bootstrappés
        double DfInterp(double t, double fallbackRate)
        {
            if (t <= 0.0) return 1.0;

            if (points.Count == 0)
                return Math.Exp(-fallbackRate * t);

            var ordered = points.OrderBy(p => p.T).ToList();

            if (t <= ordered.First().T)
                return Math.Exp(-ordered.First().ZeroRate * t);

            if (t >= ordered.Last().T)
                return Math.Exp(-ordered.Last().ZeroRate * t);

            for (int i = 0; i < ordered.Count - 1; i++)
            {
                var a = ordered[i];
                var b = ordered[i + 1];

                if (t >= a.T && t <= b.T)
                {
                    double w = (t - a.T) / (b.T - a.T);
                    double z = a.ZeroRate * (1.0 - w) + b.ZeroRate * w;
                    return Math.Exp(-z * t);
                }
            }

            return Math.Exp(-ordered.Last().ZeroRate * t);
        }

        foreach (var ins in instruments)
        {
            if (ins.Type == InstrumentType.Deposit)
            {
                // ===============================
                // 1) DÉPÔTS
                // ===============================
                double T = ins.MaturityYears;
                double dcf = T;
                double df = 1.0 / (1.0 + ins.Rate * dcf);
                double z = -Math.Log(df) / T;

                points.Add(new CurvePoint(T, z));
            }
            else if (ins.Type == InstrumentType.SWAP)
            {
                // ===============================
                // 2) SWAPS VANILLA
                // ===============================
                int freq = ins.FixedFreq;
                if (freq <= 0)
                    freq = 1;

                double T = ins.MaturityYears;
                int n = (int)Math.Round(T * freq);
                n = Math.Max(n, 1);
                double dt = 1.0 / freq;
                double S = ins.Rate;

                double F(double zT)
                {
                    double sumFix = 0.0;

                    for (int k = 1; k <= n - 1; k++)
                    {
                        double t = k * dt;
                        double df = DfInterp(t, S);
                        sumFix += df * dt;
                    }

                    double dfT = Math.Exp(-zT * T);
                    sumFix += dfT * dt;

                    double fixedLeg = S * sumFix;
                    double floatLeg = 1.0 - dfT;

                    return fixedLeg - floatLeg;
                }

                double lo = -0.05, hi = 0.20;
                double flo = F(lo);
                double fhi = F(hi);

                if (Math.Sign(flo) == Math.Sign(fhi))
                    throw new InvalidOperationException(
                        $"Impossible de borner la racine pour le swap T={T}, F(lo) et F(hi) ont le même signe."
                    );

                for (int it = 0; it < 100; it++)
                {
                    double mid = 0.5 * (lo + hi);
                    double fmid = F(mid);

                    if (Math.Abs(fmid) < 1e-12) { lo = hi = mid; break; }

                    if (Math.Sign(flo) == Math.Sign(fmid))
                    {
                        lo = mid;
                        flo = fmid;
                    }
                    else
                    {
                        hi = mid;
                        fhi = fmid;
                    }
                }

                double zSolved = 0.5 * (lo + hi);
                points.Add(new CurvePoint(T, zSolved));
            }
            else if (ins.Type == InstrumentType.BOND)
            {
                // ===============================
                // 3) OBLIGATIONS / OAT
                // ===============================
                double T = ins.MaturityYears;
                double c = ins.Coupon / 100.0;
                double y = ins.Rate;
                double P = ins.Price; // prix de marché en fraction du nominal

                if (T <= 0.0)
                    continue;

                // --- Cas zéro-coupon : flux unique à T
                if (c == 0.0)
                {
                    if (P <= 0.0 || P >= 1.5)
                        throw new InvalidOperationException(
                            $"Prix incohérent pour zéro-coupon T={T}, P={P}"
                        );

                    double z = -Math.Log(P) / T;
                    points.Add(new CurvePoint(T, z));
                    continue;
                }

                // --- Obligations à coupon ---
                int freq = ins.FixedFreq > 0 ? ins.FixedFreq : 1;

                // Nombre de paiements restant (approximation)
                int n = (int)Math.Round(T * freq);
                n = Math.Max(n, 1);

                // On étale les flux régulièrement entre 0 et T
                double dt = T / n;
                double couponCF = c * dt;

                // 1) PV des flux avant l'échéance finale, using courbe déjà bootstrappée
                double A = 0.0;
                for (int k = 1; k <= n - 1; k++)
                {
                    double t = k * dt;
                    double df = DfInterp(t, y);
                    A += couponCF * df;
                }

                // 2) Flux final : coupon + nominal
                double B = couponCF + 1.0;

                double dfT = (P - A) / B;

                if (dfT <= 0.0 || dfT >= 1.5)
                    throw new InvalidOperationException(
                        $"DF(T) incohérent pour bond T={T} : DF(T)={dfT}, P={P}, A={A}, B={B}"
                    );

                double zBond = -Math.Log(dfT) / T;
                points.Add(new CurvePoint(T, zBond));
            }
        }

        return points.OrderBy(p => p.T).ToList();
    }
}
