using System.Globalization;
using System.Text;
using RateCurveProject.Engine;
using RateCurveProject.Models;

namespace RateCurveProject.Output;

public class ExportManager
{
    private readonly string _dir;
    public ExportManager(string dir){ _dir = dir; }

    public string ExportCurve(Curve curve, string fileName, double tStart, double tEnd, double step=0.25)
    {
        var path = Path.Combine(_dir, fileName);
        using var sw = new StreamWriter(path, false, Encoding.UTF8);
        sw.WriteLine("T,Zero,DF,Forward");
        for (double t=tStart; t<=tEnd+1e-9; t+=step)
        {
            double z=curve.Zero(t);
            double df=curve.DF(t);
            double f=curve.ForwardInstantaneous(t);
            sw.WriteLine($"{t.ToString(CultureInfo.InvariantCulture)},{z.ToString(CultureInfo.InvariantCulture)},{df.ToString(CultureInfo.InvariantCulture)},{f.ToString(CultureInfo.InvariantCulture)}");
        }
        return path;
    }

    public string ExportMetrics(IEnumerable<CurveAnalyzer.Metric> metrics, string fileName)
    {
        var path = Path.Combine(_dir, fileName);
        using var sw = new StreamWriter(path, false, Encoding.UTF8);
        sw.WriteLine("T,Zero,DF,FwdInst,Slope,Convexity");
        foreach (var m in metrics)
            sw.WriteLine($"{m.T.ToString(CultureInfo.InvariantCulture)},{m.Zero.ToString(CultureInfo.InvariantCulture)},{m.DF.ToString(CultureInfo.InvariantCulture)},{m.FwdInst.ToString(CultureInfo.InvariantCulture)},{m.Slope.ToString(CultureInfo.InvariantCulture)},{m.Convexity.ToString(CultureInfo.InvariantCulture)}");
        return path;
    }
}
