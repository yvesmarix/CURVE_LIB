using RateCurveProject.Data;
using RateCurveProject.Models;

namespace RateCurveProject.Engine;

public class Bootstrapper
{
    // Simple sequential bootstrap: deposits -> swaps -> OATs
    public List<CurvePoint> BuildZeroCurve(List<MarketInstrument> instruments)
    {
        instruments = instruments.OrderBy(i => i.MaturityYears).ToList();
        var points = new List<CurvePoint>();

        foreach (var ins in instruments)
        {
            if (ins.Type == InstrumentType.Deposit)
            {
                double dcf = ins.MaturityYears;
                double df = 1.0 / (1.0 + ins.Rate * dcf);
                double zero = -Math.Log(df) / ins.MaturityYears;
                points.Add(new CurvePoint(ins.MaturityYears, zero));
            }
            else if (ins.Type == InstrumentType.Swap)
            {
                int n = (int)(ins.MaturityYears * ins.FixedFreq);
                double dt = 1.0 / ins.FixedFreq;

                Func<double, double> dfInterp = (t) =>
                {
                    if (t <= 0) return 1.0;
                    if (points.Count == 0) return Math.Exp(-ins.Rate * t);
                    var ordered = points.OrderBy(p => p.T).ToList();
                    if (t <= ordered.First().T) return Math.Exp(-ordered.First().ZeroRate * t);
                    if (t >= ordered.Last().T) return Math.Exp(-ordered.Last().ZeroRate * t);
                    for (int i = 0; i < ordered.Count - 1; i++)
                    {
                        var a = ordered[i]; var b = ordered[i + 1];
                        if (t >= a.T && t <= b.T)
                        {
                            double w = (t - a.T) / (b.T - a.T);
                            double z = a.ZeroRate * (1 - w) + b.ZeroRate * w;
                            return Math.Exp(-z * t);
                        }
                    }
                    return Math.Exp(-ordered.Last().ZeroRate * t);
                };

                double target = ins.Rate;
                double T = ins.MaturityYears;
                Func<double, double> f = (zeroT) =>
                {
                    double sum = 0.0;
                    for (int k = 1; k <= n - 1; k++) sum += dfInterp(k * dt) * dt;
                    double dfT = Math.Exp(-zeroT * T);
                    sum += dfT * dt;
                    double fixedLeg = target * sum;
                    double floatLeg = 1.0 - dfT; // approximation
                    return fixedLeg - floatLeg;
                };
                double lo = -0.05, hi = 0.20;
                for (int it = 0; it < 100; it++)
                {
                    double mid = 0.5 * (lo + hi);
                    double fmid = f(mid);
                    if (Math.Abs(fmid) < 1e-12) { lo = hi = mid; break; }
                    double flo = f(lo);
                    if (Math.Sign(flo) == Math.Sign(fmid)) lo = mid; else hi = mid;
                }
                double zeroSolved = 0.5 * (lo + hi);
                points.Add(new CurvePoint(T, zeroSolved));
            }
            else if (ins.Type == InstrumentType.OAT)
            {
                // OAT (zero-coupon bond) pricing
                double df = 1.0 / Math.Pow(1.0 + ins.Rate, ins.MaturityYears);
                double zero = -Math.Log(df) / ins.MaturityYears;
                points.Add(new CurvePoint(ins.MaturityYears, zero));
            }
        }
        return points.OrderBy(p => p.T).ToList();
    }
}
