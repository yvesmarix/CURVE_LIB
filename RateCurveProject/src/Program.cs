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
        // repli : répertoire courant (current working directory)
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
        string method = args.Length > 1 ? args[1] : "Linear";
        string outArg = args.Length > 2 ? args[2] : "OutputRuns";

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
        // également : sauvegarder une page HTML interactive à côté des PNG (infobulles au survol)
        plotter.PlotCurves(curve, "curve_plot.png", title: "Zero curve", interactive: true);
        plotter.PlotForward(curve, "forward_plot.png", title: "Instantaneous forward", interactive: true);

        // Par défaut, afficher le visualiseur GUI natif. Passez '--no-gui' (ou 'no-gui' / '-n') pour le désactiver.
        if (!args.Any(a => a == "no-gui" || a == "--no-gui" || a == "-n"))
        {
            Console.WriteLine("Ouverture du visualiseur GUI interactif pour la courbe zéro (fermez la fenêtre pour continuer)...");
            plotter.ShowInteractiveWinForms(curve, title: $"Zero curve ({interp.GetType().Name})", mode: "zero");

            Console.WriteLine("Ouverture du visualiseur GUI interactif pour la courbe forward instantané (fermez la fenêtre pour terminer)...");
            plotter.ShowInteractiveWinForms(curve, title: $"Forward instantané ({interp.GetType().Name})", mode: "forward");
        }

        Console.WriteLine($"Done. Outputs in: {outputDir}");
    }
}