namespace RateCurveProject.Models.Interpolation;

using RateCurveProject.Models;

/// <summary>
/// Implémente une interpolation linéaire simple sur les zéro-taux.
/// La courbe est représentée par des segments de droite reliant les points (T, ZeroRate) fournis.
/// </summary>
public class LinearInterpolator : IInterpolator
{
    private List<CurvePoint> pts = new();

    /// <summary>
    /// Prépare l'interpolateur en stockant et triant les points de la courbe par maturité.
    /// </summary>
    /// <param name="points">La liste des points (T, ZeroRate) bruts.</param>
    public void Build(IReadOnlyList<CurvePoint> points)
    {
        if (points == null || points.Count == 0)
            throw new ArgumentException("La liste de points ne peut pas être vide.", nameof(points));

        pts = points.OrderBy(p => p.T).ToList();
    }

    /// <summary>
    /// Évalue le zéro-taux à une maturité `t` donnée par interpolation linéaire.
    /// - Si `t` est en dehors des bornes, une extrapolation plate est appliquée (la valeur du premier ou du dernier point est retournée).
    /// - Sinon, le taux est interpolé linéairement entre les deux points les plus proches.
    /// </summary>
    /// <param name="t">La maturité (en années) à laquelle évaluer le taux.</param>
    /// <returns>Le zéro-taux interpolé.</returns>
    public double Eval(double t)
    {
        if (pts.Count == 0)
            throw new InvalidOperationException("L'interpolateur doit être construit avec des points avant d'être évalué.");

        // Extrapolation plate avant le premier point
        if (t <= pts.First().T) return pts.First().ZeroRate;
        // Extrapolation plate après le dernier point
        if (t >= pts.Last().T) return pts.Last().ZeroRate;

        // Recherche de l'intervalle et interpolation linéaire
        for (int i = 0; i < pts.Count - 1; i++)
        {
            var a = pts[i];
            var b = pts[i+1];
            if (t >= a.T && t <= b.T)
            {
                // w est le poids d'interpolation, variant de 0 (à t=a.T) à 1 (à t=b.T)
                double w = (t - a.T) / (b.T - a.T);
                return a.ZeroRate * (1 - w) + b.ZeroRate * w;
            }
        }

        // Cas de repli (ne devrait pas être atteint si la logique est correcte)
        return pts.Last().ZeroRate;
    }
}
