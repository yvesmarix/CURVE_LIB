using RateCurveProject.Models;

namespace RateCurveProject.Engine;

public class CurveAnalyzer
{
    private readonly Curve _curve;
    public CurveAnalyzer(Curve curve){ _curve = curve; }

    public record Metric(double T, double Zero, double DF, double FwdInst, double Slope, double Convexity);

    public List<Metric> ComputeMetrics(IEnumerable<double> tenorsYears)
    {
        var res = new List<Metric>();
        foreach (var t in tenorsYears)
        {
            double z = _curve.Zero(t);
            double df = _curve.DF(t);
            double f = _curve.ForwardInstantaneous(t);
            double h=1e-3;
            double z1=_curve.Zero(Math.Max(t-h, 1e-6));
            double z2=_curve.Zero(t+h);
            double slope=(z2-z1)/(2*h);
            double zpp=(_curve.Zero(t+h) - 2*_curve.Zero(t) + _curve.Zero(Math.Max(t-h,1e-6)))/(h*h);
            res.Add(new Metric(t, z, df, f, slope, zpp));
        }
        return res;
    }
}
