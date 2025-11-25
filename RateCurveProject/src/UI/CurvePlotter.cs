using System.Diagnostics;
using RateCurveProject.Models;

namespace RateCurveProject.UI;

public class CurvePlotter
{
    private readonly string _dir;
    public CurvePlotter(string dir) { _dir = dir; }

    /// <summary>
    /// Enregistre un PNG soigné affichant la courbe zéro-taux et les facteurs d'actualisation.
    /// Le paramètre `title` permet d'ajouter un nom lisible pour la courbe dans le titre du graphique.
    /// Si `title` est null on génère un titre par défaut qui inclut le nom de l'interpolateur.
    /// </summary>
    public void PlotCurves(Curve curve, string fileName, string? title = null, double tStart = 0.0, double tEnd = 30.0, double step = 0.1)
    {
        var plt = new ScottPlot.Plot();

        var xs = new List<double>();
        var zero = new List<double>();
        var df = new List<double>();
        for (double t = tStart; t <= tEnd; t += step)
        {
            xs.Add(t);
            zero.Add(curve.Zero(t) * 100.0);
            df.Add(curve.DF(t));
        }

        var sZero = plt.Add.Scatter(xs.ToArray(), zero.ToArray());
        sZero.Label = "Taux zéro (%)";
        sZero.LineWidth = 2;
        sZero.MarkerSize = 0; // trace en ligne (pas de marqueurs)

        var sDf = plt.Add.Scatter(xs.ToArray(), df.ToArray());
        sDf.Label = "Facteur d'actualisation";
        sDf.LineWidth = 2;
        sDf.MarkerSize = 0;

        // ScottPlot v5: configuration via propriétés (API moderne)
        // Ajoute le nom de l'interpolateur (si présent) pour rendre évident
        // quelle méthode d'interpolation a été utilisée dans le titre du graphique.
        var interpName = curve.InterpolatorName ?? string.Empty;
        if (!string.IsNullOrEmpty(title)) title = $"{title} ({interpName})";
        else title = $"Courbe de taux & DF ({interpName})";

        plt.Axes.Title.Label.Text = title;
        plt.Axes.Bottom.Label.Text = "Maturité (années)";
        plt.Axes.Left.Label.Text = "Niveau";
        plt.ShowLegend();

        string path = Path.Combine(_dir, fileName);
        // Enregistre un PNG haute qualité pour visualisation et partage
        plt.SavePng(path, 1000, 600);
        // (HTML interactive export removed) — seule la sauvegarde PNG reste.
    }

    public void PlotForward(Curve curve, string fileName, string? title = null, double tStart = 0.0, double tEnd = 30.0, double step = 0.1)
    {
        var plt = new ScottPlot.Plot();

        var xs = new List<double>();
        var fwd = new List<double>();
        for (double t = tStart; t <= tEnd; t += step)
        {
            xs.Add(t);
            fwd.Add(curve.ForwardInstantaneous(t) * 100.0);
        }

        var sFwd = plt.Add.Scatter(xs.ToArray(), fwd.ToArray());
        sFwd.Label = "Forward instantané (%)"; // étiquette en français déjà
        sFwd.LineWidth = 2;

        var interpNameFwd = curve.InterpolatorName ?? string.Empty;
        if (!string.IsNullOrEmpty(title)) title = $"{title} ({interpNameFwd})";
        else title = $"Forward instantané ({interpNameFwd})";

        plt.Axes.Title.Label.Text = title;
        plt.Axes.Bottom.Label.Text = "Maturité (années)";
        plt.Axes.Left.Label.Text = "%";

        plt.ShowLegend();

        string path = Path.Combine(_dir, fileName);
        plt.SavePng(path, 1000, 600);
        // (HTML interactive export removed) — seule la sauvegarde PNG reste.
    }

    /// <summary>
    /// Crée une page HTML interactive (Plotly.js) et l'ouvre dans le navigateur par défaut.
    /// Fonctionne sur macOS, Windows, Linux (tant qu'il y a un navigateur).
    /// - mode = "zero"  : affiche la courbe zéro-taux + DF
    /// - mode = "forward": affiche la courbe de forward instantané
    /// </summary>
    public void ShowInteractiveBrowser(
        Curve curve,
        string fileNameHtml = "courbe_interactive.html",
        string mode = "zero",
        double tStart = 0.0,
        double tEnd = 30.0,
        double step = 0.1)
    {
        var xs = new List<double>();
        var ys = new List<double>();
        var dfs = new List<double>();

        for (double t = tStart; t <= tEnd; t += step)
        {
            xs.Add(t);

            if (mode == "forward")
            {
                ys.Add(curve.ForwardInstantaneous(t) * 100.0); // en %
            }
            else
            {
                ys.Add(curve.Zero(t) * 100.0); // en %
                dfs.Add(curve.DF(t));
            }
        }

        // Sérialisation simple en JS: [1.0,2.0,3.0]
        string JsArray(IEnumerable<double> arr)
            => "[" + string.Join(",", arr.Select(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture))) + "]";

        string xsJs = JsArray(xs);
        string ysJs = JsArray(ys);
        string dfsJs = mode == "forward" ? "[]" : JsArray(dfs);

        string interpName = curve.InterpolatorName ?? "";
        string title = mode == "forward"
            ? $"Forward instantané ({interpName})"
            : $"Courbe de taux & DF ({interpName})";

        // HTML minimal avec Plotly.js depuis un CDN
        string html = $@"
        <!DOCTYPE html>
        <html lang=""fr"">
        <head>
            <meta charset=""utf-8"" />
            <title>{title}</title>
            <script src=""https://cdn.plot.ly/plotly-2.30.0.min.js""></script>
        </head>
        <body>
            <h2 style=""font-family:sans-serif;"">{title}</h2>
            <div id=""chart"" style=""width:100%;height:600px;""></div>
            <script>
                const xs = {xsJs};
                const ys = {ysJs};
                const dfs = {dfsJs};

                const traceRate = {{
                    x: xs,
                    y: ys,
                    mode: 'lines',
                    name: '{(mode == "forward" ? "Forward (%)" : "Taux zéro (%)")}'
                }};

                let data = [traceRate];

                {(mode == "forward" ? "" : @"
                const traceDf = {
                    x: xs,
                    y: dfs,
                    mode: 'lines',
                    name: 'Discount factor',
                    yaxis: 'y2'
                };
                data.push(traceDf);
                ")}

                const layout = {{
                    xaxis: {{ title: 'Maturité (années)' }},
                    yaxis: {{ title: '{(mode == "forward" ? "Forward instantané (%)" : "Taux zéro (%)")}' }},
                    {(mode == "forward" ? "" : "yaxis2: { title: 'DF', overlaying: 'y', side: 'right' },")}
                    legend: {{ orientation: 'h', x: 0.0, y: -0.2 }},
                    margin: {{ t: 40, l: 60, r: 60, b: 60 }}
                }};

                Plotly.newPlot('chart', data, layout);
            </script>
        </body>
        </html>";

        // Écrit le fichier HTML dans le répertoire de sortie
        string path = Path.Combine(_dir, fileNameHtml);
        File.WriteAllText(path, html);

        // Ouvre dans le navigateur par défaut (cross-platform)
        var psi = new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        };
        Process.Start(psi);
    }
}

