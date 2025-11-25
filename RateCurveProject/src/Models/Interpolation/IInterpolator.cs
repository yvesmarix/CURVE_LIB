namespace RateCurveProject.Models.Interpolation;

using RateCurveProject.Models;

/// <summary>
/// Définit le contrat pour les stratégies d'interpolation de courbe.
/// Un interpolateur est capable de se construire à partir d'un ensemble de points,
/// puis d'évaluer la courbe à n'importe quelle maturité.
/// </summary>
public interface IInterpolator
{
    /// <summary>
    /// Construit ou calibre l'interpolateur à partir d'une liste de points de courbe.
    /// </summary>
    /// <param name="points">Liste de points (T, ZeroRate) servant de base à l'interpolation.</param>
    void Build(IReadOnlyList<CurvePoint> points);

    /// <summary>
    /// Évalue la valeur interpolée (généralement un zéro-taux) à une maturité `t` donnée.
    /// </summary>
    /// <param name="t">La maturité (en années) à laquelle évaluer la courbe.</param>
    /// <returns>La valeur interpolée à la maturité `t`.</returns>
    double Eval(double t);
}
