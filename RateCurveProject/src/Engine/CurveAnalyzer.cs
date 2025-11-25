using RateCurveProject.Models;

namespace RateCurveProject.Engine;

/// <summary>
/// Fournit des routines d'analyse numérique pour une courbe de taux.
/// Cette classe calcule, pour une liste de maturités :
/// - le zéro-taux continu Z(t),
/// - le facteur d'actualisation DF(t) = exp(-Z(t)*t),
/// - le forward instantané approximé,
/// - la dérivée première (slope) et la dérivée seconde (convexity) de Z(t) via différences finies.
/// Les résultats sont renvoyés dans des instances immuables de <see cref="Metric"/>.
/// </summary>
public class CurveAnalyzer
{
    private readonly Curve _curve;
    /// <summary>
    /// Crée un analyseur attaché à la courbe fournie.
    /// </summary>
    /// <param name="curve">Objet <see cref="Curve"/> dont on analysera les propriétés.</param>
    public CurveAnalyzer(Curve curve)
    {
        _curve = curve ?? throw new ArgumentNullException(nameof(curve));
    }

    /// <summary>
    /// Structure de retour contenant les métriques calculées pour une maturité donnée.
    /// Champs :
    /// - T : maturité (années)
    /// - Zero : zéro-taux continu Z(T) (fraction, ex: 0.03 == 3%)
    /// - DF : discount factor DF(T) = exp(-Z(T) * T)
    /// - FwdInst : forward instantané approximé à T
    /// - Slope : dérivée première (approx. de Z'(T)) calculée par différence centrale
    /// - Convexity : dérivée seconde (approx. de Z''(T)) calculée par différences finies
    /// </summary>
    public record Metric(double T, double Zero, double DF, double FwdInst, double Slope, double Convexity);

    /// <summary>
    /// Calcule les métriques pour chaque maturité listée dans <paramref name="tenorsYears"/>.
    ///
    /// Méthode numérique :
    /// - la dérivée première (slope) est estimée par différence centrale : (Z(t+h) - Z(t-h)) / (2h)
    /// - la dérivée seconde (convexity) est estimée par la formule centrée : (Z(t+h) - 2 Z(t) + Z(t-h)) / h^2
    ///
    /// Remarques :
    /// - on évite d'évaluer la courbe pour t <= 0 en clampant la borne inférieure à 1e-6 pour la sécurité numérique.
    /// - le pas h est fixé ici à 1e-3 (suffisamment petit pour une estimation raisonnable mais évite le bruit numérique).
    /// </summary>
    /// <param name="tenorsYears">Maturités (en années) pour lesquelles calculer les métriques.</param>
    /// <returns>Liste des métriques calculées pour chaque maturité.</returns>
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
