using RateCurveProject.Models;
using RateCurveProject.Models.Interpolation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RateCurveProject.Tests;

[TestClass]
public class InterpolationTests
{
    /// <summary>
    /// Arrange: Créer une interpolation linéaire entre deux points
    /// Act: Évaluer aux limites et au point médian
    /// Assert: Les valeurs doivent correspondre exactement (midpoint = moyenne)
    /// </summary>
    [TestMethod]
    public void LinearInterpolatorShouldEvaluateCorrectlyAtBoundariesAndMidpoint()
    {
        // Arrange
        var point1 = new CurvePoint(0.0, 0.01);
        var point2 = new CurvePoint(2.0, 0.03);
        var points = new[] { point1, point2 };
        var interpolator = new LinearInterpolator();
        interpolator.Build(points);

        // Act & Assert
        // À t=0: doit retourner 0.01
        Assert.AreEqual(0.01, interpolator.Eval(0.0), 0.000000000001, "Interpolation à t=0 échouée");
        
        // À t=2: doit retourner 0.03
        Assert.AreEqual(0.03, interpolator.Eval(2.0), 0.000000000001, "Interpolation à t=2 échouée");
        
        // À t=1 (midpoint): doit retourner la moyenne = (0.01 + 0.03) / 2 = 0.02
        Assert.AreEqual(0.02, interpolator.Eval(1.0), 0.000000000001, "Interpolation au midpoint échouée");
    }

    /// <summary>
    /// Arrange: Créer une spline cubique avec 2 points seulement
    /// Act: Évaluer aux limites et au point médian
    /// Assert: Doit se comporter linéairement (2 points insuffisent pour une vraie spline)
    /// </summary>
    [TestMethod]
    public void CubicSplineInterpolatorShouldBehaveLikeLinearOnTwoPoints()
    {
        // Arrange
        var point1 = new CurvePoint(0.0, 0.01);
        var point2 = new CurvePoint(2.0, 0.03);
        var points = new[] { point1, point2 };
        var interpolator = new CubicSplineInterpolator();
        interpolator.Build(points);

        // Act & Assert
        // Aux extrémités: doit passer par les points exactement
        Assert.AreEqual(0.01, interpolator.Eval(0.0), 0.000000000001, "CubicSpline à t=0 échouée");
        Assert.AreEqual(0.03, interpolator.Eval(2.0), 0.000000000001, "CubicSpline à t=2 échouée");
        
        // Au midpoint: doit être très proche de 0.02 (comportement linéaire)
        double midpointValue = interpolator.Eval(1.0);
        Assert.IsTrue(midpointValue >= 0.0199 && midpointValue <= 0.0201, 
            $"CubicSpline au midpoint doit être entre 0.0199 et 0.0201, trouvé: {midpointValue}");
    }
}
