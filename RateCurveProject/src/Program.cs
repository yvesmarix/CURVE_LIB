using System.Diagnostics;                    // 👈 Ajouter ceci
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

    /// <summary>
    /// Ouvre un fichier (HTML, etc.) avec l'application par défaut du système.
    /// </summary>
    static void OpenInBrowser(string path)
    {
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"⚠ Fichier introuvable, impossible d'ouvrir : {path}");
            return;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true   // laisse l'OS choisir le navigateur / appli
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"⚠ Impossible d'ouvrir {path} : {ex.Message}");
        }
    }

    static void Main(string[] args)
    {
        Console.WriteLine("RateCurveProject - Construction & Lissage de Courbe");

        // 1) Résoudre chemins robustement
        string projectRoot = FindProjectRoot();

        // Déterminer le pays (menu interactif si pas d'argument)
        string country = "France";
        if (args.Length > 0)
        {
            country = args[0].ToLower() switch
            {
                "us" => "US",
                "france" => "France",
                _ => "France"
            };
        }
        else
        {
            // Menu interactif
            Console.WriteLine("\n Sélectionnez le marché de taux:");
            Console.WriteLine("  [1] France");
            Console.WriteLine("  [2] États-Unis (US)");
            Console.Write("\nVotre choix (1 ou 2): ");

            string? choice = Console.ReadLine();
            country = choice?.Trim() switch
            {
                "2" or "us" or "US" => "US",
                "1" or "france" or "france" => "France",
                _ => "France"
            };
        }

        // Déterminer le répertoire de sortie
        string outArg = args.Length > 1 ? args[1] : "OutputRuns";

        string outputDir = ResolveOutputDir(outArg, projectRoot);
        Directory.CreateDirectory(outputDir);

        // 2) Charger données
        var loader = new MarketDataLoader();
        var dataPath = Path.Combine(
            projectRoot,
            "src",
            "Samples",
            $"data_{country}.xlsx"
        );

        Console.WriteLine($"\n Données chargées depuis: {country}");
        Console.WriteLine($"Fichier: {dataPath}");

        if (!File.Exists(dataPath))
        {
            Console.Error.WriteLine($"Erreur: Le fichier {dataPath} n'existe pas!");
            return;
        }

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

        // Afficher les points de la courbe zéro
        Console.WriteLine("\nPoints de la courbe zéro bootstrappée :");
        var zeroTable = new ConsoleTable("Maturité (ans)", "Taux Zéro", "Facteur d'Actualisation");
        foreach (var point in zeroPoints)
        {
            double discountFactor = Math.Exp(-point.ZeroRate * point.T);
            zeroTable.AddRow(
                point.T.ToString("F2"),
                point.ZeroRate.ToString("P4"),
                discountFactor.ToString("F6")
            );
        }
        zeroTable.Write();

        // 4) Liste des méthodes d'interpolation à tester
        var methodNames = new[] { "Linear", "CubicSpline", "HaganWest", "SmithWilson" };

        foreach (var method in methodNames)
        {
            Console.WriteLine();
            Console.WriteLine($"=== Méthode d'interpolation : {method} ===");

            // Sous-dossier par pays et méthode : OutputRuns/France/Linear, OutputRuns/US/Linear, ...
            string countryDir = Path.Combine(outputDir, country);
            string methodDir = Path.Combine(countryDir, method);
            Directory.CreateDirectory(methodDir);

            // suffix pour tous les noms de fichiers
            string suffix = "_" + method;

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

            // 6) Exports CSV & HTML (un fichier par méthode, nom avec suffixe)
            var exporter = new ExportManager(methodDir);
            try
            {
                exporter.ExportCurve(curve, $"curve_points{suffix}.csv", 0.25, 30.0);
                exporter.ExportMetrics(metrics, $"metrics{suffix}.csv");

                // On garde le nom de fichier dans une variable
                string metricsHtmlFileName = $"metrics{suffix}.html";
                exporter.ExportMetricsHtml(metrics, metricsHtmlFileName);

                // Chemin absolu pour ouverture dans le navigateur
                string metricsHtmlFullPath = Path.Combine(methodDir, metricsHtmlFileName);
                OpenInBrowser(metricsHtmlFullPath);     // 👈 ouverture auto du metrics.html
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Erreur lors de l'export pour {method}: {ex.Message}");
            }

            // 7) Visualisation (PNG + HTML interactif) dans le sous-dossier
            var plotter = new CurvePlotter(methodDir);

            // PNG statiques, nommés avec suffixe
            plotter.PlotCurves(curve, $"curve_plot{suffix}.png", title: $"Zero curve - {method}");
            plotter.PlotForward(curve, $"forward_plot{suffix}.png", title: $"Instantaneous forward - {method}");

            // Graph interactif (navigateur) : HTML avec suffixe méthode
            plotter.ShowInteractiveBrowser(curve, $"courbe_zero_interactive{suffix}.html", mode: "zero");
            plotter.ShowInteractiveBrowser(curve, $"courbe_forward_interactive{suffix}.html", mode: "forward");
        }

        Console.WriteLine();
        Console.WriteLine($"Done. Outputs in: {outputDir}/{country}");
    }
}
