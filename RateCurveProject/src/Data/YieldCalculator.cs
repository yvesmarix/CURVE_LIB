public static class BondYieldCalculator
{
    // couponPct : coupon en % (ex: 3.5)
    // maturityYears : Maturity (ta colonne, ex: 7.01)
    // pricePct : Ask Price en % (ex: 102.186)
    public static double ComputeYield(double couponPct, double maturityYears, double pricePct)
    {
        double P = pricePct / 100.0;     // prix en fraction de nominal
        double c = couponPct / 100.0;    // coupon en fraction de nominal

        // Cas zero-coupon : formule analytique
        if (couponPct == 0.0)
        {
            return Math.Pow(1.0 / P, 1.0 / maturityYears) - 1.0;
        }

        // Approximation simple : nombre de flux annuels
        int N = Math.Max(1, (int)Math.Round(maturityYears));

        // Fonction f(y) = PV(coupons+principal) - P
        double F(double y)
        {
            double pv = 0.0;
            for (int k = 1; k <= N; k++)
                pv += c / Math.Pow(1.0 + y, k);

            pv += 1.0 / Math.Pow(1.0 + y, N); // nominal
            return pv - P;
        }

        // Recherche par bissection sur un intervalle raisonnable
        double lo = -0.05;  // -5%
        double hi = 0.20;   // 20%

        for (int iter = 0; iter < 100; iter++)
        {
            double mid = 0.5 * (lo + hi);
            double fmid = F(mid);

            if (Math.Abs(fmid) < 1e-10)
                return mid;

            double flo = F(lo);
            if (Math.Sign(flo) == Math.Sign(fmid))
                lo = mid;
            else
                hi = mid;
        }

        return 0.5 * (lo + hi); // approx finale
    }
}
