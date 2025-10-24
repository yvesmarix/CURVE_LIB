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
    static void Main(string[] args)
    {
        Console.WriteLine("RateCurveProject - Construction & Lissage de Courbe");

        // 1) Résoudre chemins robustement
        string projectRoot = FindProjectRoot();
        string csvArg = args.Length > 0 ? args[0] : "";
        string method = args.Length > 1 ? args[1] : "HaganWest";
        string outArg = args.Length > 2 ? args[2] : "OutputRuns";

        string instrumentsCsv = ResolveCsvPath(csvArg, projectRoot);
        string outputDir = ResolveOutputDir(outArg, projectRoot);
        Directory.CreateDirectory(outputDir);

        // 2) Charger données
        var loader = new MarketDataLoader();
        var quotes = loader.LoadInstruments(instrumentsCsv);
        Console.WriteLine("Instruments chargés :");

        // Créez un tableau formaté avec ConsoleTable
        var table = new ConsoleTable("Type", "Maturité (ans)", "Taux", "DayCount", "Fréquence Fixe");
        foreach (var quote in quotes)
        {
            table.AddRow(quote.Type, quote.MaturityYears, quote.Rate, quote.DayCount, quote.FixedFreq);
        }

        // Affichez le tableau dans le terminal
        table.Write();

        // 3) Bootstrap
        var bootstrapper = new Bootstrapper();
        var zeroPoints = bootstrapper.BuildZeroCurve(quotes);

        // 4) Choix interpolateur
        IInterpolator interp = method switch
        {
            "Linear" => new LinearInterpolator(),
            "CubicSpline" => new CubicSplineInterpolator(),
            "SmithWilson" => new SmithWilsonInterpolator(ultimateForwardRate: 0.025, lambda: 0.1),
            _ => new HaganWestInterpolator(), // défaut
        };
        var curve = new Curve(zeroPoints, interp);

        // 5) Analyse
        var analyzer = new CurveAnalyzer(curve);
        var metrics = analyzer.ComputeMetrics(new double[] { 0.25, 0.5, 1, 2, 3, 5, 7, 10, 15, 20, 30 });

        // 6) Exports
        var exporter = new ExportManager(outputDir);
        exporter.ExportCurve(curve, "curve_points.csv", 0.25, 30.0);
        exporter.ExportMetrics(metrics, "metrics.csv");

        // 7) Visualisation
        var plotter = new CurvePlotter(outputDir);
        plotter.PlotCurves(curve, "curve_plot.png");
        plotter.PlotForward(curve, "forward_plot.png");

        Console.WriteLine($"Done. Outputs in: {outputDir}");
    }

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

    static string ResolveCsvPath(string csvArg, string projectRoot)
    {
        if (!string.IsNullOrWhiteSpace(csvArg) && File.Exists(csvArg))
            return Path.GetFullPath(csvArg);

        // Essais relatifs fréquents
        var candidates = new[]
        {
            csvArg,
            Path.Combine(projectRoot, "src", "Samples", "instruments_sample.csv"),
            Path.Combine(projectRoot, "Samples", "instruments_sample.csv"),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "Samples", "instruments_sample.csv")
        }.Where(s => !string.IsNullOrWhiteSpace(s));

        foreach (var c in candidates)
            if (File.Exists(c)) return c;

        throw new FileNotFoundException($"CSV introuvable. Essayé: {string.Join(", ", candidates)}");
    }

    static string ResolveOutputDir(string outArg, string projectRoot)
    {
        if (Path.IsPathRooted(outArg))
            return outArg;

        // Place les sorties à la racine du projet (pas dans bin/)
        return Path.Combine(projectRoot, outArg);
    }
}
