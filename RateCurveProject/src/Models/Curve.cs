namespace RateCurveProject.Models;

public record CurvePoint(double T, double ZeroRate);

public class Curve
{
    private readonly List<CurvePoint> _points;
    private readonly Interpolation.IInterpolator _interp;

    public Curve(IEnumerable<CurvePoint> points, Interpolation.IInterpolator interpolator)
    {
        _points = points.OrderBy(p => p.T).ToList();
        _interp = interpolator;
        _interp.Build(_points);
    }

    /// <summary>
    /// Human-friendly name of the interpolator implementation used by this curve.
    /// Useful for titles, labels and diagnostics.
    /// </summary>
    public string InterpolatorName => _interp?.GetType().Name ?? "UnknownInterpolator";

    public double Zero(double t) => _interp.Eval(t);
    public double DF(double t) => Math.Exp(-Zero(t) * t);
    public double ForwardInstantaneous(double t, double h = 1e-4)
    {
        var p1 = DF(Math.Max(t - h, 1e-6));
        var p2 = DF(t + h);
        return -(Math.Log(p2) - Math.Log(p1)) / (2*h);
    }

    public IReadOnlyList<CurvePoint> RawPoints => _points;
}
