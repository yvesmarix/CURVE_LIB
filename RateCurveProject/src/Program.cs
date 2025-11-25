using RateCurveProject.Data;
using RateCurveProject.Engine;
using RateCurveProject.Models;
using RateCurveProject.Models.Interpolation;
using RateCurveProject.Output;
using RateCurveProject.UI;
using ConsoleTables;

namespace RateCurveProject;

public class Program
{
    // Trouve le dossier racine du projet (celui qui contient le dossier 'src')
    static string FindProjectRoot()
    {
        // Point de départ: dossier binaire (bin/Debug/netX.Y)
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 10 && dir != null; i++)
        {
            // Cherche 'src' juste en dessous
            if (dir.GetDirectories("src").Any())
                return dir.FullName;
            dir = dir.Parent;
        }
        // fallback: cwd
        return Directory.GetCurrentDirectory();
    }

    static string ResolveOutputDir(string outArg, string projectRoot)
    {
        if (Path.IsPathRooted(outArg))
            return outArg;

        // Place les sorties à la racine du projet (pas dans bin/)
        return Path.Combine(projectRoot, outArg);
    }

    static void Main(string[] args)
    {
        Console.WriteLine("RateCurveProject - Construction & Lissage de Courbe");

        // 1) Résoudre chemins robustement
        string projectRoot = FindProjectRoot();
        // on ne lit plus la méthode dans les args, on sort TOUS les graphes
        string outArg = args.Length > 1 ? args[1] : "OutputRuns";

        string outputDir = ResolveOutputDir(outArg, projectRoot);
        Directory.CreateDirectory(outputDir);

        // 2) Charger données
        var loader = new MarketDataLoader();
        var dataPath = Path.Combine(
            projectRoot,
            "src",
            "Samples",
            "data_france.xlsx"
        );

        var quotes = loader.LoadInstruments(dataPath);
        Console.WriteLine("Instruments chargés :");

        // Créez un tableau formaté avec ConsoleTable
        var table = new ConsoleTable("Type", "Maturité (ans)", "Taux", "Fréquence Fixe", "Coupon");
        foreach (var quote in quotes)
        {
            table.AddRow(quote.Type, quote.MaturityYears, quote.Rate, quote.FixedFreq, quote.Coupon);
        }

        // Affichez le tableau dans le terminal
        table.Write();

        // 3) Bootstrap (unique) de la courbe zéro à partir des instruments
        var bootstrapper = new Bootstrapper();
        var zeroPoints = bootstrapper.BuildZeroCurve(quotes);

        // 4) Liste des méthodes d'interpolation à tester
        var methodNames = new[] { "Linear", "CubicSpline", "HaganWest", "SmithWilson" };

        foreach (var method in methodNames)
        {
            Console.WriteLine();
            Console.WriteLine($"=== Méthode d'interpolation : {method} ===");

            // Sous-dossier par méthode : OutputRuns/Linear, OutputRuns/CubicSpline, ...
            string methodDir = Path.Combine(outputDir, method);
            Directory.CreateDirectory(methodDir);

            // 4a) Choix interpolateur
            IInterpolator interp = method switch
            {
                "Linear" => new LinearInterpolator(),
                "CubicSpline" => new CubicSplineInterpolator(),
                "SmithWilson" => new SmithWilsonInterpolator(ultimateForwardRate: 0.025, lambda: 0.1),
                _ => new HaganWestInterpolator(), // défaut → Hagan-West
            };

            var curve = new Curve(zeroPoints, interp);

            // 5) Analyse (mêmes maturités de référence pour toutes les méthodes)
            var analyzer = new CurveAnalyzer(curve);
            var metrics = analyzer.ComputeMetrics(new double[] { 0.25, 0.5, 1, 2, 3, 5, 7, 10, 15, 20, 30 });

            // 6) Exports CSV (un fichier par méthode, dans son sous-dossier)
            var exporter = new ExportManager(methodDir);
            exporter.ExportCurve(curve, "curve_points.csv", 0.25, 30.0);
            exporter.ExportMetrics(metrics, "metrics.csv");

            // 7) Visualisation (PNG + HTML interactif) dans le sous-dossier
            var plotter = new CurvePlotter(methodDir);

            // PNG statiques
            plotter.PlotCurves(curve, "curve_plot.png", title: $"Zero curve - {method}");
            plotter.PlotForward(curve, "forward_plot.png", title: $"Instantaneous forward - {method}");

            // Graph interactif (navigateur) : un HTML pour la zéro, un pour les forwards
            plotter.ShowInteractiveBrowser(curve, "courbe_zero_interactive.html", mode: "zero");
            plotter.ShowInteractiveBrowser(curve, "courbe_forward_interactive.html", mode: "forward");
        }

        Console.WriteLine();
        Console.WriteLine($"Done. Outputs in: {outputDir}");
    }
}
