using RateCurveProject.Output;
using RateCurveProject.Models;
using RateCurveProject.Models.Interpolation;
using RateCurveProject.Engine;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace RateCurveProject.Tests;

[TestClass]
public class ExportManagerTests
{
    /// <summary>
    /// Arrange: Créer une courbe simple et un gestionnaire d'export
    /// Act: Exporter la courbe et les métriques en CSV
    /// Assert: Vérifier que les fichiers sont créés avec les bons en-têtes CSV
    /// </summary>
    [TestMethod]
    public void ExportManagerShouldExportCurveAndMetricsWithCorrectCsvHeaders()
    {
        // Arrange
        // Créer un répertoire temporaire pour les tests
        string tempDirectory = Path.Combine(Path.GetTempPath(), "ratecurve_tests");
        if (!Directory.Exists(tempDirectory))
        {
            Directory.CreateDirectory(tempDirectory);
        }

        var exporter = new ExportManager(tempDirectory);

        // Créer une courbe simple
        var point1 = new CurvePoint(0.5, 0.01);
        var point2 = new CurvePoint(1.0, 0.02);
        var points = new[] { point1, point2 };
        var interpolator = new LinearInterpolator();
        interpolator.Build(points);
        var curve = new Curve(points, interpolator);

        try
        {
            // Act & Assert - Export courbe
            // Exporter la courbe en CSV
            string curveFilePath = exporter.ExportCurve(curve, "tc_curve.csv", 0.5, 1.0, step: 0.5);
            
            // Le fichier doit exister
            Assert.IsTrue(File.Exists(curveFilePath), $"Le fichier de courbe n'existe pas: {curveFilePath}");
            
            // Lire les lignes du fichier
            string[] curveLines = File.ReadAllLines(curveFilePath);
            
            // Doit avoir au moins l'en-tête + 1 ligne de données
            Assert.IsTrue(curveLines.Length >= 2, $"Le fichier de courbe doit avoir au moins 2 lignes, trouvé: {curveLines.Length}");
            
            // L'en-tête doit être correct
            string expectedCurveHeader = "T,Zero,DF,Forward";
            Assert.AreEqual(expectedCurveHeader, curveLines[0], "En-tête de courbe incorrect");

            // Act & Assert - Export métriques
            // Calculer les métriques
            var analyzer = new CurveAnalyzer(curve);
            var tenors = new double[] { 0.5, 1.0 };
            var metrics = analyzer.ComputeMetrics(tenors);
            
            // Exporter les métriques
            string metricsFilePath = exporter.ExportMetrics(metrics, "tc_metrics.csv");
            
            // Le fichier doit exister
            Assert.IsTrue(File.Exists(metricsFilePath), $"Le fichier de métriques n'existe pas: {metricsFilePath}");
            
            // Lire les lignes du fichier
            string[] metricsLines = File.ReadAllLines(metricsFilePath);
            
            // L'en-tête doit être correct
            string expectedMetricsHeader = "T,Zero,DF,FwdInst,Slope,Convexity";
            Assert.AreEqual(expectedMetricsHeader, metricsLines[0], "En-tête de métriques incorrect");
        }
        finally
        {
            // Cleanup: supprimer les fichiers de test
            string curveFile = Path.Combine(tempDirectory, "tc_curve.csv");
            string metricsFile = Path.Combine(tempDirectory, "tc_metrics.csv");
            
            if (File.Exists(curveFile)) File.Delete(curveFile);
            if (File.Exists(metricsFile)) File.Delete(metricsFile);
        }
    }
}
