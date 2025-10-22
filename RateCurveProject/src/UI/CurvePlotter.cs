using ScottPlot;
using RateCurveProject.Models;

namespace RateCurveProject.UI;

public class CurvePlotter
{
    private readonly string _dir;
    public CurvePlotter(string dir) { _dir = dir; }

    public void PlotCurves(Curve curve, string fileName, double tStart = 0.0, double tEnd = 30.0, double step = 0.1)
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

        var sDf = plt.Add.Scatter(xs.ToArray(), df.ToArray());
        sDf.Label = "Discount factor";

        // ScottPlot v5: propriétés (pas de méthodes)
        plt.Axes.Title.Label.Text = "Courbe de taux & DF";
        plt.Axes.Bottom.Label.Text = "Maturité (années)";
        plt.Axes.Left.Label.Text = "Niveau";

        plt.ShowLegend();

        string path = Path.Combine(_dir, fileName);
        plt.SavePng(path, 1000, 600);
    }

    public void PlotForward(Curve curve, string fileName, double tStart = 0.0, double tEnd = 30.0, double step = 0.1)
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

        plt.Axes.Title.Label.Text = "Forward instantané";
        plt.Axes.Bottom.Label.Text = "Maturité (années)";
        plt.Axes.Left.Label.Text = "%";

        plt.ShowLegend();

        string path = Path.Combine(_dir, fileName);
        plt.SavePng(path, 1000, 600);
    }
}
