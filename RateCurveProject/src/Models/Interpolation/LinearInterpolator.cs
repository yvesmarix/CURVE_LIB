namespace RateCurveProject.Models.Interpolation;

using RateCurveProject.Models;

public class LinearInterpolator : IInterpolator
{
    private List<CurvePoint> pts = new();

    public void Build(IReadOnlyList<CurvePoint> points)
    {
        pts = points.OrderBy(p => p.T).ToList();
    }

    public double Eval(double t)
    {
        if (t <= pts.First().T) return pts.First().ZeroRate;
        if (t >= pts.Last().T) return pts.Last().ZeroRate;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            var a = pts[i];
            var b = pts[i+1];
            if (t >= a.T && t <= b.T)
            {
                double w = (t - a.T) / (b.T - a.T);
                return a.ZeroRate * (1 - w) + b.ZeroRate * w;
            }
        }
        return pts.Last().ZeroRate;
    }
}
