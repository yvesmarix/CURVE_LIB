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
    /// Save a nicely styled PNG showing the zero curve and discount factors.
    /// Pass `title` to include a human-friendly curve name in the plot.
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
        sZero.MarkerSize = 0; // line plot

        var sDf = plt.Add.Scatter(xs.ToArray(), df.ToArray());
        sDf.Label = "Discount factor";
        sDf.LineWidth = 2;
        sDf.MarkerSize = 0;

        // ScottPlot v5: propriétés (pas de méthodes)
        // Append interpolator name if provided by the Curve object, so titles clearly show which method was used.
        var interpName = curve.InterpolatorName ?? string.Empty;
        if (!string.IsNullOrEmpty(title)) title = $"{title} ({interpName})";
        else title = $"Courbe de taux & DF ({interpName})";

        plt.Axes.Title.Label.Text = title;
        plt.Axes.Bottom.Label.Text = "Maturité (années)";
        plt.Axes.Left.Label.Text = "Niveau";
        plt.ShowLegend();

        string path = Path.Combine(_dir, fileName);
        // Save a high-quality PNG and also an SVG for better scaling when inspecting plots
        plt.SavePng(path, 1000, 600);
        // create an optional small interactive HTML viewer that can be opened in a browser.
        if (interactive)
        {
            var pngPath = path;
            var htmlPath = Path.ChangeExtension(path, "html");
            var yMin = zero.Min();
            var yMax = zero.Max();
            // Use percentage label for zero curve
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
        sFwd.Label = "Forward instantané (%)";
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
            // forward values are percentages in our plot
            SaveInteractiveHtml(xs, fwd, pngPath, htmlPath, tStart, tEnd, yMin, yMax, 1000, 600, title, "%");
        }
    }

    /// <summary>
    /// Opens an interactive window (WinForms) displaying the curve.
    /// Hovering the mouse shows the nearest data point (T, value).
    /// This method only runs on Windows with WinForms support.
    /// </summary>
        /// <summary>
        /// Produce a small interactive HTML page next to the saved PNG.
        /// The HTML contains a copy of the exported PNG plus a lightweight JavaScript overlay
        /// that maps the cursor position to the data coordinates and displays the nearest point values.
        /// This works cross-platform without requiring WinForms or a GUI dependency.
        /// </summary>
        public void SaveInteractiveHtml(IEnumerable<double> xs, IEnumerable<double> ys, string pngPath, string htmlPath, double xMin, double xMax, double yMin, double yMax, int width = 1000, int height = 600, string? title = null, string? yUnit = null)
        {
                var xsJson = System.Text.Json.JsonSerializer.Serialize(xs);
                var ysJson = System.Text.Json.JsonSerializer.Serialize(ys);

                var displayTitle = string.IsNullOrEmpty(title) ? "Interactive plot" : title.Replace("\"", "\'");
                var displayUnit = string.IsNullOrEmpty(yUnit) ? "" : yUnit;

                var html = $@"<!doctype html>
<html>
<head>
    <meta charset='utf-8'/>
    <title>Interactive plot</title>
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
            const px = e.clientX - rect.left; // px inside image
            const py = e.clientY - rect.top;
            const plotW = rect.width; const plotH = rect.height;
            // map to data coordinates assuming full image is plot area
            const x = xMin + (px / plotW) * (xMax - xMin);
            const y = yMax - (py / plotH) * (yMax - yMin); // inverted y-axis from pixels
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

                // write html and copy png next to it if necessary
                var dir = Path.GetDirectoryName(htmlPath) ?? Directory.GetCurrentDirectory();
                var pngName = Path.GetFileName(pngPath);
                // If pngPath points elsewhere, copy PNG to same directory for the HTML to load by relative name
                var dstPng = Path.Combine(dir, pngName);
                if (!string.Equals(Path.GetFullPath(dstPng), Path.GetFullPath(pngPath)))
                        File.Copy(pngPath, dstPng, overwrite: true);

                File.WriteAllText(htmlPath, html);
        }

            /// <summary>
            /// Show a native WinForms interactive viewer using ScottPlot.FormsPlot.
            /// The viewer supports zoom/pan (built-in) and a native hover tooltip showing the nearest point value.
            /// </summary>
                /// <summary>
                /// Show an interactive WinForms viewer using ScottPlot.FormsPlot.
                /// mode = "zero" (default) draws the zero curve; mode = "forward" draws instantaneous forward.
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

                // ScottPlot WinForms control: native, interactive (zoom/pan), with precise mouse->data mapping
                var formsPlot = new ScottPlot.WinForms.FormsPlot();
                formsPlot.Dock = DockStyle.Fill;
                form.Controls.Add(formsPlot);

                // status / tooltip label at bottom
                var status = new System.Windows.Forms.Label();
                status.Dock = DockStyle.Bottom;
                status.Height = 28;
                status.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
                status.Padding = new System.Windows.Forms.Padding(6, 0, 0, 0);
                    status.Text = mode == "forward" ? "Hover the plot to see t / forward (%)" : "Hover the plot to see t / rate";
                form.Controls.Add(status);

                var plt = formsPlot.Plot;
                plt.Title(form.Text);
                plt.XLabel("Maturité (ans)");
                    plt.YLabel(mode == "forward" ? "Forward instantané (%)" : "Taux zéro (%)");

                var s = plt.Add.Scatter(xs.ToArray(), ys.ToArray());
                s.LineWidth = 2;

                // add markers at raw points
                var rawX = curve.RawPoints.Select(p => p.T).ToArray();
                var rawY = curve.RawPoints.Select(p => p.ZeroRate * 100.0).ToArray();
                var markers = plt.Add.Scatter(rawX, rawY);
                markers.LineWidth = 0;
                markers.MarkerSize = 6;

                // highlight point to show nearest sample
                var highlight = plt.Add.Scatter(new double[] { 0 }, new double[] { 0 });
                highlight.MarkerSize = 10;
                // leave highlight color default (avoid cross-type assignments)

                // refresh the control
                formsPlot.Refresh();

                // handle mouse movement on the formsPlot (gives precise data coordinates)
                int lastIdx = -1;
                formsPlot.MouseMove += (snd, e) =>
                {
                    try
                    {
                        // get precise data coordinates using the native API
                        // convert mouse pixel to data coordinates using Plot.GetCoordinates
                        var coords = plt.GetCoordinates((float)e.Location.X, (float)e.Location.Y, plt.Axes.Bottom, plt.Axes.Left);
                        double mx = coords.X;
                        double my = coords.Y;
                        // nearest index on xs
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

                                // update highlight point in the plot without recreating the whole image (fast)
                                if (idx != lastIdx)
                                {
                                    lastIdx = idx;
                                    // rebuild the minimal plot state with the new highlight (fast enough when index changes only)
                                    plt.Clear();
                                    plt.Add.Scatter(xs.ToArray(), ys.ToArray()).LineWidth = 2;
                                    plt.Add.Scatter(rawX, rawY).LineWidth = 0; // markers
                                    highlight = plt.Add.Scatter(new double[] { t }, new double[] { val });
                                    highlight.MarkerSize = 10;
                                    formsPlot.Refresh();
                                }
                    }
                    catch
                    {
                        // ignore transient errors when pointer is outside axes etc
                    }
                };

                // show window
                Application.EnableVisualStyles();
                Application.Run(form);
            }
}
