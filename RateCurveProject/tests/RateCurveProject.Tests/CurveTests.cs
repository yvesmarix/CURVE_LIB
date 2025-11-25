using RateCurveProject.Models;
using RateCurveProject.Models.Interpolation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RateCurveProject.Tests;

[TestClass]
public class CurveTests
{
    /// <summary>
    /// Arrange: Créer une courbe linéaire (Z(t) = 0.02 à 0.04)
    /// Act: Évaluer les taux zéro, les facteurs de discount et la forward instantanée
    /// Assert: Vérifier que Zero, DF, et Forward retournent des valeurs valides
    /// </summary>
    [TestMethod]
    public void CurveShouldComputeCorrectlyZeroRatesAndDiscountFactors()
    {
        // Arrange
        var point1 = new CurvePoint(1.0, 0.02);
        var point2 = new CurvePoint(3.0, 0.04);
        var points = new[] { point1, point2 };
        var interpolator = new LinearInterpolator();
        interpolator.Build(points);
        var curve = new Curve(points, interpolator);

        // Act & Assert - Taux zéro
        // Les taux zéro doivent correspondre à l'interpolateur
        Assert.AreEqual(0.02, curve.Zero(1.0), 0.000000000001, "Taux zéro à t=1.0 échoué");
        Assert.AreEqual(0.04, curve.Zero(3.0), 0.000000000001, "Taux zéro à t=3.0 échoué");

        // Act & Assert - Facteurs de discount
        // DF(t) = exp(-Z(t) * t)
        double expectedDF_at_1 = Math.Exp(-0.02 * 1.0);
        Assert.AreEqual(expectedDF_at_1, curve.DF(1.0), 0.000000000001, "Facteur de discount à t=1.0 échoué");

        // Act & Assert - Forward instantanée
        // Pour une courbe zéro linéaire, le forward doit être un nombre fini (pas NaN ni Infinity)
        double forwardInstantaneous = curve.ForwardInstantaneous(2.0);
        Assert.IsFalse(double.IsNaN(forwardInstantaneous), "Forward instantanée ne doit pas être NaN");
        Assert.IsFalse(double.IsInfinity(forwardInstantaneous), "Forward instantanée ne doit pas être Infinity");
    }
}
