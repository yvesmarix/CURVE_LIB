using RateCurveProject.Data;
using RateCurveProject.Models;

namespace RateCurveProject.Engine;

/// <summary>
/// Bootstrap d'une courbe zéro-coupon à partir de :
/// - dépôts (instruments money-market)
/// - SWAPs vanilla
/// - BONDs (OAT) avec coupon
/// 
/// Hypothèses fortes :
/// - Les coupons des BOND sont payés annuellement (FixedFreq = 1 si Coupon > 0).
/// - La courbe est interpolée linéairement en zéro-taux entre les points déjà bootstrappés.
/// - La valeur nominale est 1.
/// - MarketInstrument.Rate = yield actuariel (pour les BOND) ou taux de marché (pour les dépôts / swaps).
/// </summary>
public class Bootstrapper
{
    public List<CurvePoint> BuildZeroCurve(List<MarketInstrument> instruments)
    {
        // On travaille avec les instruments triés par maturité croissante.
        instruments = instruments.OrderBy(i => i.MaturityYears).ToList();

        // Liste des points (T, Z(T)) de la courbe zéro-coupon résultante.
        var points = new List<CurvePoint>();

        // Fonction locale d'interpolation de discount factors DF(t)
        double DfInterp(double t, double fallbackRate)
        {
            if (t <= 0.0) return 1.0;

            // Si on n'a encore aucun point sur la courbe,
            // on utilise un simple DF = exp(-r * t) avec r = fallbackRate de l'instrument courant.
            if (points.Count == 0)
                return Math.Exp(-fallbackRate * t);

            var ordered = points.OrderBy(p => p.T).ToList();

            // Avant le premier point : extrapolation plate en zéro-taux
            if (t <= ordered.First().T)
                return Math.Exp(-ordered.First().ZeroRate * t);

            // Après le dernier point : extrapolation plate en zéro-taux
            if (t >= ordered.Last().T)
                return Math.Exp(-ordered.Last().ZeroRate * t);

            // Entre deux points : interpolation linéaire en zéro-taux
            for (int i = 0; i < ordered.Count - 1; i++)
            {
                var a = ordered[i];
                var b = ordered[i + 1];

                if (t >= a.T && t <= b.T)
                {
                    double w = (t - a.T) / (b.T - a.T);        // poids d'interpolation
                    double z = a.ZeroRate * (1 - w) + b.ZeroRate * w;
                    return Math.Exp(-z * t);
                }
            }

            // Fallback (ne devrait pas arriver)
            return Math.Exp(-ordered.Last().ZeroRate * t);
        }

        foreach (var ins in instruments)
        {
            if (ins.Type == InstrumentType.Deposit)
            {
                // ===============================
                // 1) DÉPÔTS (money-market)
                // ===============================
                //
                // Convention simple : DF(T) = 1 / (1 + R * T)
                // puis Z(T) = - ln(DF(T)) / T

                double T = ins.MaturityYears;
                double dcf = T;                   // fraction de l'année ~ maturité (ACT/365 simplifié)
                double df = 1.0 / (1.0 + ins.Rate * dcf);
                double z = -Math.Log(df) / T;

                points.Add(new CurvePoint(T, z));
            }
            else if (ins.Type == InstrumentType.SWAP)
            {
                // ===============================
                // 2) SWAPS VANILLA
                // ===============================
                //
                // On suppose :
                // - fixed leg payé à fréquence FixedFreq (annuelle, semestrielle, ...)
                // - floating leg approximé par 1 - DF(T)
                // - taux de swap ins.Rate (taux fixe)
                //
                // On résout numériquement pour le zéro-taux Z(T) tel que :
                // Sum_k (DF(t_k) * dt * S) = 1 - DF(T)
                // où S = ins.Rate

                int freq = ins.FixedFreq;                   // ex: 1 ou 2
                double T = ins.MaturityYears;
                int n = (int)(T * freq);
                double dt = 1.0 / freq;
                double S = ins.Rate;                        // swap rate cible

                // Fonction f(Z(T)) = PV(fixed leg) - PV(float leg)
                double F(double zT)
                {
                    double sumFix = 0.0;

                    // Coupons fixes avant l'échéance finale
                    for (int k = 1; k <= n - 1; k++)
                    {
                        double t = k * dt;
                        double df = DfInterp(t, S);             // DF interpolé à partir des points existants
                        sumFix += df * dt;
                    }

                    // Dernier DF(T) dépend du zéro-taux inconnu zT
                    double dfT = Math.Exp(-zT * T);
                    sumFix += dfT * dt;

                    double fixedLeg = S * sumFix;
                    double floatLeg = 1.0 - dfT;                // approximation : PV du float = 1 - DF(T)

                    return fixedLeg - floatLeg;
                }

                // Résolution par bissection sur un intervalle raisonnable
                double lo = -0.05, hi = 0.20;
                for (int it = 0; it < 100; it++)
                {
                    double mid = 0.5 * (lo + hi);
                    double fmid = F(mid);

                    if (Math.Abs(fmid) < 1e-12) { lo = hi = mid; break; }

                    double flo = F(lo);
                    if (Math.Sign(flo) == Math.Sign(fmid))
                        lo = mid;
                    else
                        hi = mid;
                }

                double zSolved = 0.5 * (lo + hi);
                points.Add(new CurvePoint(T, zSolved));
            }
            else if (ins.Type == InstrumentType.BOND)
            {
                // ===============================
                // 3) BONDS / OAT AVEC COUPON
                // ===============================
                //
                // Ici on UTILISE le coupon de l'OAT.
                // Hypothèse : FixedFreq = 1 si coupon > 0 (fréquence annuelle).
                //
                // Étapes :
                //   1) On reconstruit le prix "de marché" P à partir du yield ins.Rate
                //      (marketInstrument.Rate est supposé être un YTM).
                //   2) On impose que ce prix P soit égal à la somme actualisée des flux
                //      en utilisant la courbe déjà bootstrappée pour les flux antérieurs
                //      et un zéro-taux inconnu Z(T) pour le dernier DF(T).
                //   3) On résout pour Z(T) par bissection.
                //
                // Remarque : si le coupon est nul (vraie zéro-coupon), on revient
                // à la formule fermée DF(T) = 1 / (1 + y)^T.

                double T = ins.MaturityYears;
                double c = ins.Coupon / 100.0;       // coupon en fraction de nominal
                double y = ins.Rate;                 // yield actuariel de l'OAT
                int freq = ins.FixedFreq > 0 ? ins.FixedFreq : 1;
                double dt = 1.0 / freq;
                int n = Math.Max(1, (int)Math.Round(T * freq));

                // --- Cas zéro-coupon : on utilise la formule fermée directement
                if (c == 0.0)
                {
                    // Prix implicite à partir du yield
                    double Pzc = 1.0 / Math.Pow(1.0 + y, T);
                    double z = -Math.Log(Pzc) / T;
                    points.Add(new CurvePoint(T, z));
                    continue;
                }

                // --- 3.1 Prix implicite P à partir du yield y (standard bond pricing)
                double P = 0.0;
                for (int k = 1; k <= n; k++)
                {
                    double t = k * dt;
                    // Discount par le yield y (on suppose y nominal annualisé avec même freq)
                    double dfY = 1.0 / Math.Pow(1.0 + y / freq, k);
                    P += c * dt * dfY;
                }
                P += 1.0 / Math.Pow(1.0 + y / freq, n);   // remboursement du nominal

                // --- 3.2 Fonction f(Z(T)) = PV(cashflows avec courbe) - P
                double Fbond(double zT)
                {
                    double pvCoupons = 0.0;

                    // Coupons avant l'échéance finale : on utilise DF interpolés
                    for (int k = 1; k <= n - 1; k++)
                    {
                        double t = k * dt;
                        double df = DfInterp(t, y);   // fallback rate = yield
                        pvCoupons += c * dt * df;
                    }

                    // Dernier coupon + nominal à T, actualisé avec Z(T)
                    double dfT = Math.Exp(-zT * T);
                    pvCoupons += (c * dt + 1.0) * dfT;

                    return pvCoupons - P;
                }

                // --- 3.3 Résolution par bissection pour trouver Z(T)
                double loB = -0.05, hiB = 0.20;
                for (int it = 0; it < 100; it++)
                {
                    double mid = 0.5 * (loB + hiB);
                    double fmid = Fbond(mid);

                    if (Math.Abs(fmid) < 1e-12) { loB = hiB = mid; break; }

                    double flo = Fbond(loB);
                    if (Math.Sign(flo) == Math.Sign(fmid))
                        loB = mid;
                    else
                        hiB = mid;
                }

                double zBond = 0.5 * (loB + hiB);
                points.Add(new CurvePoint(T, zBond));
            }
        }

        // On s'assure que la liste finale est bien triée en maturité
        return points.OrderBy(p => p.T).ToList();
    }
}
