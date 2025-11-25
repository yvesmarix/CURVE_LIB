using ScottPlot;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using ScottPlot.WinForms;
using RateCurveProject.Models;
using System.Linq;

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
    public void PlotCurves(Curve curve, string fileName, string? title = null, double tStart = 0.0, double tEnd = 30.0, double step = 0.1, bool interactive = false)
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
        // Optionnel : génère une petite page HTML interactive (PNG + JS overlay)
        // qui peut être ouverte dans un navigateur pour obtenir des info-bulles au survol.
        if (interactive)
        {
            var pngPath = path;
            var htmlPath = Path.ChangeExtension(path, "html");
            var yMin = zero.Min();
            var yMax = zero.Max();
            // Les taux zéro sont affichés en pourcentage
            SaveInteractiveHtml(xs, zero, pngPath, htmlPath, tStart, tEnd, yMin, yMax, 1000, 600, title, "%");
        }
    }

    public void PlotForward(Curve curve, string fileName, string? title = null, double tStart = 0.0, double tEnd = 30.0, double step = 0.1, bool interactive = false)
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
        if (interactive)
        {
            var pngPath = path;
            var htmlPath = Path.ChangeExtension(path, "html");
            var yMin = fwd.Min();
            var yMax = fwd.Max();
                // les valeurs forward sont exprimées en pourcentage dans nos graphiques
            SaveInteractiveHtml(xs, fwd, pngPath, htmlPath, tStart, tEnd, yMin, yMax, 1000, 600, title, "%");
        }
    }

    /// <summary>
    /// Ouvre une fenêtre interactive (WinForms) affichant la courbe.
    /// Le survol de la souris affiche le point de données le plus proche (t, valeur).
    /// Remarque : cette méthode fonctionne uniquement sous Windows avec le support WinForms.
    /// </summary>
        /// <summary>
        /// Génère une page HTML interactive côté client à côté du PNG exporté.
        /// La page contient le PNG et un petit script JavaScript qui :
        /// - mappe la position du curseur sur les coordonnées de données,
        /// - recherche le point le plus proche, et
        /// - affiche une infobulle (tooltip) avec les valeurs correspondantes.
        /// Utile quand on veut une vue interactive multiplateforme sans dépendance GUI.
        /// </summary>
        public void SaveInteractiveHtml(IEnumerable<double> xs, IEnumerable<double> ys, string pngPath, string htmlPath, double xMin, double xMax, double yMin, double yMax, int width = 1000, int height = 600, string? title = null, string? yUnit = null)
        {
                var xsJson = System.Text.Json.JsonSerializer.Serialize(xs);
                var ysJson = System.Text.Json.JsonSerializer.Serialize(ys);

                var displayTitle = string.IsNullOrEmpty(title) ? "Graphique interactif" : title.Replace("\"", "\'");
                var displayUnit = string.IsNullOrEmpty(yUnit) ? "" : yUnit;

                var html = $@"<!doctype html>
<html>
<head>
    <meta charset='utf-8'/>
    <title>Graphique interactif</title>
    <style>
        body {{ font-family: Arial, Helvetica, sans-serif; margin: 0; padding: 0; display:flex; justify-content:center; }}
        .plot {{ position: relative; width: {width}px; height: {height}px; }}
        .plot img {{ width: 100%; height: 100%; display:block; }}
        .tooltip {{ position: absolute; background: rgba(255,255,255,0.9); border: 1px solid #ccc; padding: 6px; pointer-events: none; display:none; font-size: 13px; }}
    </style>
</head>
<body>
    <div style='text-align:center; margin:10px;'>
        <strong>{displayTitle}</strong>
    </div>
    <div class='plot'>
        <img src='{Path.GetFileName(pngPath)}' alt='plot'/>
        <div id='tt' class='tooltip'></div>
    </div>
    <script>
        const xs = {xsJson};
        const ys = {ysJson};
        const xMin = {xMin};
        const xMax = {xMax};
        const yMin = {yMin};
        const yMax = {yMax};
        const plot = document.querySelector('.plot');
        const img = plot.querySelector('img');
        const tt = document.getElementById('tt');
        function findNearest(x) {{
            let best = 0; let bestd = Infinity;
            for (let i = 0; i < xs.length; i++) {{
                const d = Math.abs(xs[i] - x);
                if (d < bestd) {{ bestd = d; best = i; }}
            }}
            return best;
        }}
        plot.addEventListener('mousemove', function (e) {{
            const rect = img.getBoundingClientRect();
            const px = e.clientX - rect.left; // px à l'intérieur de l'image
            const py = e.clientY - rect.top;
            const plotW = rect.width; const plotH = rect.height;
            // conversion vers coordonnées de données en supposant que toute l'image correspond à la zone de tracé
            const x = xMin + (px / plotW) * (xMax - xMin);
            const y = yMax - (py / plotH) * (yMax - yMin); // axe Y inversé par rapport aux pixels (origine en haut)
            const idx = findNearest(x);
            tt.style.display = 'block';
            tt.style.left = (px + 12) + 'px';
            tt.style.top = (py + 12) + 'px';
            tt.innerHTML = 't = ' + xs[idx].toFixed(4) + '<br/>' + ys[idx].toFixed(6) + ' {displayUnit}';
        }});
        plot.addEventListener('mouseleave', function () {{ tt.style.display = 'none'; }});
    </script>
</body>
</html>";

                    // écrit le fichier HTML et copie le PNG à côté si nécessaire
                var dir = Path.GetDirectoryName(htmlPath) ?? Directory.GetCurrentDirectory();
                var pngName = Path.GetFileName(pngPath);
                // Si pngPath est situé ailleurs, on copie le PNG dans le répertoire cible
                // pour que la page HTML puisse le charger via un nom relatif.
                var dstPng = Path.Combine(dir, pngName);
                if (!string.Equals(Path.GetFullPath(dstPng), Path.GetFullPath(pngPath)))
                        File.Copy(pngPath, dstPng, overwrite: true);

                File.WriteAllText(htmlPath, html);
        }

            /// <summary>
            /// Affiche une vue native WinForms interactive en utilisant ScottPlot.FormsPlot.
            /// Le contrôle offre zoom/pan natif et une infobulle native montrant la valeur du point le plus proche.
            /// </summary>
                /// <summary>
                /// Affiche un visualiseur WinForms interactif (ScottPlot.FormsPlot).
                /// - mode = "zero" (par défaut) dessine la courbe zéro-taux
                /// - mode = "forward" dessine la courbe forward instantanée
                /// </summary>
                public void ShowInteractiveWinForms(Curve curve, string? title = null, string mode = "zero", double tStart = 0.0, double tEnd = 30.0, double step = 0.1)
            {
                // build data
                var xs = new List<double>();
                var ys = new List<double>();
                for (double t = tStart; t <= tEnd; t += step)
                {
                    xs.Add(t);
                        if (mode == "forward") ys.Add(curve.ForwardInstantaneous(t) * 100.0);
                        else ys.Add(curve.Zero(t) * 100.0);
                }

                // form
                var form = new Form();
                form.Text = title ?? ($"Curve ({curve.InterpolatorName})");
                form.StartPosition = FormStartPosition.CenterScreen;
                form.ClientSize = new Size(1100, 700);

                // Contrôle WinForms ScottPlot : natif, interactif (zoom/pan) et conversion précise souris->données
                var formsPlot = new ScottPlot.WinForms.FormsPlot();
                formsPlot.Dock = DockStyle.Fill;
                form.Controls.Add(formsPlot);

                // étiquette de statut / infobulle en bas
                var status = new System.Windows.Forms.Label();
                status.Dock = DockStyle.Bottom;
                status.Height = 28;
                status.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
                status.Padding = new System.Windows.Forms.Padding(6, 0, 0, 0);
                        status.Text = mode == "forward" ? "Survolez le graphique pour afficher t / forward (%)" : "Survolez le graphique pour afficher t / taux (%)";
                form.Controls.Add(status);

                var plt = formsPlot.Plot;
                plt.Title(form.Text);
                plt.XLabel("Maturité (ans)");
                    plt.YLabel(mode == "forward" ? "Forward instantané (%)" : "Taux zéro (%)");

                var s = plt.Add.Scatter(xs.ToArray(), ys.ToArray());
                s.LineWidth = 2;

                // ajoute des marqueurs aux points bruts échantillonnés
                var rawX = curve.RawPoints.Select(p => p.T).ToArray();
                var rawY = curve.RawPoints.Select(p => p.ZeroRate * 100.0).ToArray();
                var markers = plt.Add.Scatter(rawX, rawY);
                markers.LineWidth = 0;
                markers.MarkerSize = 6;

                // point de surbrillance utilisé pour indiquer l'échantillon le plus proche
                var highlight = plt.Add.Scatter(new double[] { 0 }, new double[] { 0 });
                highlight.MarkerSize = 10;
                // laisser la couleur de surbrillance par défaut (éviter les conversions de types de couleur)

                // rafraîchit le contrôle pour s'assurer que le rendu initial est appliqué
                formsPlot.Refresh();

                // gestion du mouvement de la souris sur formsPlot (fournit des coordonnées de données précises)
                int lastIdx = -1;
                formsPlot.MouseMove += (snd, e) =>
                {
                    try
                    {
                        // obtenir des coordonnées de données précises via l'API native
                        // conversion depuis les coordonnées pixels de la souris vers les coordonnées (x,y) du graphique
                        var coords = plt.GetCoordinates((float)e.Location.X, (float)e.Location.Y, plt.Axes.Bottom, plt.Axes.Left);
                        double mx = coords.X;
                        double my = coords.Y;
                        // recherche de l'indice du point le plus proche dans xs
                        int idx = 0;
                        double best = double.MaxValue;
                        for (int i = 0; i < xs.Count; i++)
                        {
                            var d = Math.Abs(xs[i] - mx);
                            if (d < best) { best = d; idx = i; }
                        }

                        double t = xs[idx];
                        double val = ys[idx];
                            status.Text = mode == "forward"
                                ? $"t = {t:0.000}  forward = {val:0.000}% (interpolator: {curve.InterpolatorName})"
                                : $"t = {t:0.000}  rate = {val:0.000}% (interpolator: {curve.InterpolatorName})";

                                // met à jour uniquement le point de surbrillance dans le tracé
                                // (évite de recréer l'image complète — opération rapide si seul l'indice change)
                                if (idx != lastIdx)
                                {
                                    lastIdx = idx;
                                    // reconstruit un état minimal du tracé avec le nouveau point surligné
                                    plt.Clear();
                                    plt.Add.Scatter(xs.ToArray(), ys.ToArray()).LineWidth = 2;
                                    plt.Add.Scatter(rawX, rawY).LineWidth = 0; // marqueurs
                                    highlight = plt.Add.Scatter(new double[] { t }, new double[] { val });
                                    highlight.MarkerSize = 10;
                                    formsPlot.Refresh();
                                }
                    }
                    catch
                    {
                        // ignorer les erreurs transitoires (curseur hors axes, etc.)
                    }
                };

                // afficher la fenêtre
                Application.EnableVisualStyles();
                Application.Run(form);
            }
}
