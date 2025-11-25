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
    /// Ouvre une fenêtre interactive (WinForms) affichant la courbe.
    /// Le survol de la souris affiche le point de données le plus proche (t, valeur).
    /// Remarque : cette méthode fonctionne uniquement sous Windows avec le support WinForms.
    /// </summary>
        // HTML export helper removed — the project now only writes PNGs and uses native WinForms viewer.

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
                // build data (principale + discount-factors)
                var xs = new List<double>();
                var ys = new List<double>();
                // pour le mode "forward" on n'affiche PAS les DF dans la popup
                List<double>? dfs = mode == "forward" ? null : new List<double>();
                for (double t = tStart; t <= tEnd; t += step)
                {
                    xs.Add(t);
                        if (mode == "forward") ys.Add(curve.ForwardInstantaneous(t) * 100.0);
                        else ys.Add(curve.Zero(t) * 100.0);
                    if (dfs != null)
                        dfs.Add(curve.DF(t));
                }

                
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
                s.Axes.YAxis = plt.Axes.Left;

                // Si nous sommes en mode ZERO (courbe zéro-taux), ajouter la série DF
                if (dfs != null)
                {
                    var sDf = plt.Add.Scatter(xs.ToArray(), dfs.ToArray());
                    sDf.LineWidth = 2;
                    sDf.Color = Colors.Orange;
                    var yAxis2 = plt.Axes.AddRightAxis();
                    yAxis2.Label.Text = "Facteur d'actualisation";
                    sDf.Axes.YAxis = yAxis2;
                }

                // ajoute des marqueurs aux points bruts échantillonnés
                var rawX = curve.RawPoints.Select(p => p.T).ToArray();
                var rawY = curve.RawPoints.Select(p => p.ZeroRate * 100.0).ToArray();
                var markers = plt.Add.Scatter(rawX, rawY);
                markers.LineWidth = 0;
                markers.MarkerSize = 6;

                // Force l'axe X à s'étirer de 0 à 30 ans pour une meilleure visualisation.
                plt.Axes.SetLimits(left: 0, right: 30);

                // point de surbrillance utilisé pour indiquer l'échantillon le plus proche
                // On utilise un 'Marker' simple, plus léger à déplacer qu'un 'Scatter'.
                var highlight = plt.Add.Marker(0, 0);
                highlight.IsVisible = false; // Caché au début
                highlight.MarkerStyle.Size = 12;
                highlight.MarkerStyle.Fill.Color = Colors.Red;
                highlight.MarkerStyle.Outline.Color = Colors.Red;

                // rafraîchit le contrôle pour s'assurer que le rendu initial est appliqué
                formsPlot.Refresh();

                // gestion du mouvement de la souris sur formsPlot (fournit des coordonnées de données précises)
                int lastIdx = -1;
                formsPlot.MouseMove += (snd, e) =>
                {
                    try
                    {
                        highlight.IsVisible = true;
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
                        if (mode == "forward")
                        {
                            status.Text = $"t = {t:0.000}  forward = {val:0.000}% (interpolator: {curve.InterpolatorName})";
                        }
                        else
                        {
                            // montrer aussi la valeur DF quand on est sur la courbe zéro
                            var df = dfs != null ? dfs[idx] : double.NaN;
                            status.Text = $"t = {t:0.000}  rate = {val:0.000}%  |  DF = {df:0.0000}  (interpolator: {curve.InterpolatorName})";
                        }

                                // Mettre à jour la position du point de surbrillance sans redessiner tout le graphique.
                                // C'est beaucoup plus performant et évite le scintillement.
                                if (idx != lastIdx)
                                {
                                    lastIdx = idx;
                                    // Déplacer le marqueur de surbrillance vers la nouvelle position.
                                    highlight.Location = new ScottPlot.Coordinates(t, val);
                                    formsPlot.Refresh();
                                }
                    }
                    catch (Exception)
                    {
                        // ignorer les erreurs transitoires (curseur hors axes, etc.)
                    }
                };

                formsPlot.MouseLeave += (s, e) =>
                {
                    highlight.IsVisible = false;
                    formsPlot.Refresh();
                };

                // afficher la fenêtre
                Application.EnableVisualStyles();
                Application.Run(form);
            }
}
