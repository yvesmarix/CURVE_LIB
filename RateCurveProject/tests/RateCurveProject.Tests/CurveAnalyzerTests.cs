using RateCurveProject.Models;
using RateCurveProject.Models.Interpolation;
using RateCurveProject.Engine;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RateCurveProject.Tests;

[TestClass]
public class CurveAnalyzerTests
{
    /// <summary>
    /// Arrange: Créer une courbe linéaire Z(t) = 0.01 + 0.01 * t (pente = 0.01)
    /// Act: Calculer les métriques (pente, convexité) pour plusieurs tenors
    /// Assert: La pente doit être ~0.01 et la convexité ~0 (2e dérivée d'une ligne = 0)
    /// </summary>
    [TestMethod]
    public void CurveAnalyzerShouldComputeMetricsWithCorrectSlopeAndConvexityForLinearCurve()
    {
        // Arrange
        // Créer une courbe linéaire: Z(t) = 0.01 + 0.01*t
        // Point 1: t=0, Z=0.01
        // Point 2: t=10, Z=0.11
        // Pente théorique = (0.11 - 0.01) / (10 - 0) = 0.01
        var point1 = new CurvePoint(0.0, 0.01);
        var point2 = new CurvePoint(10.0, 0.11);
        var points = new[] { point1, point2 };
        var interpolator = new LinearInterpolator();
        interpolator.Build(points);
        var curve = new Curve(points, interpolator);
        var analyzer = new CurveAnalyzer(curve);

        // Act
        // Évaluer les métriques sur plusieurs tenors
        var tenors = new double[] { 1.0, 2.0, 5.0, 8.0 };
        var metrics = analyzer.ComputeMetrics(tenors);

        // Assert
        // Pour chaque tenor, vérifier la pente et la convexité
        foreach (var metric in metrics)
        {
            // La pente d'une courbe linéaire doit être constante ≈ 0.01
            Assert.IsTrue(metric.Slope >= 0.009 && metric.Slope <= 0.011, 
                $"Pente doit être entre 0.009 et 0.011, trouvé: {metric.Slope}");
            
            // La convexité (2e dérivée) d'une ligne droite est zéro
            Assert.IsTrue(metric.Convexity >= -1e-6 && metric.Convexity <= 1e-6, 
                $"Convexité doit être entre -1e-6 et 1e-6, trouvé: {metric.Convexity}");
        }
    }
}
