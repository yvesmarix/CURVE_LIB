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
    /// Nom lisible de l'implémentation d'interpolateur utilisée par cette courbe.
    /// Ce nom est pratique pour les titres, les libellés d'export et le diagnostic.
    /// </summary>
    public string InterpolatorName => _interp?.GetType().Name ?? "UnknownInterpolator";

    /// <summary>
    /// Évalue le zéro-taux continu interpolé à la maturité t (exprimé en années).
    /// Résultat retourné en fraction (ex: 0.03 pour 3%).
    /// </summary>
    public double Zero(double t) => _interp.Eval(t);
    /// <summary>
    /// Retourne le facteur d'actualisation DF(t) = exp(-Z(t) * t).
    /// Utilisé pour les calculs de prix et pour tracer la série de discount-factors.
    /// </summary>
    public double DF(double t) => Math.Exp(-Zero(t) * t);
    /// <summary>
    /// Calcule le forward instantané approximé à t par une dérivée centrale
    /// sur les discount-factors : - (log DF(t+h) - log DF(t-h)) / (2h).
    /// Retourne le forward en fraction (ex: 0.01 pour 1%).
    /// </summary>
    public double ForwardInstantaneous(double t, double h = 1e-4)
    {
        var p1 = DF(Math.Max(t - h, 1e-6));
        var p2 = DF(t + h);
        return -(Math.Log(p2) - Math.Log(p1)) / (2*h);
    }

    public IReadOnlyList<CurvePoint> RawPoints => _points;
}
