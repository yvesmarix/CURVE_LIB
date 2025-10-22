// Monotone convex (Hagan-West style) implemented via monotone cubic Hermite (Fritsch-Carlson) on zero rates
namespace RateCurveProject.Models.Interpolation;

using RateCurveProject.Models;

public class HaganWestInterpolator : IInterpolator
{
    private double[] x = Array.Empty<double>();
    private double[] y = Array.Empty<double>();
    private double[] m = Array.Empty<double>(); // slopes

    public void Build(IReadOnlyList<CurvePoint> points)
    {
        x = points.Select(p => p.T).ToArray();
        y = points.Select(p => p.ZeroRate).ToArray();
        int n = x.Length;
        double[] d = new double[n-1];
        for (int i = 0; i < n-1; i++) d[i] = (y[i+1]-y[i])/(x[i+1]-x[i]);
        m = new double[n];
        m[0] = d[0];
        m[n-1] = d[n-2];
        for (int i=1;i<n-1;i++)
        {
            if (d[i-1]*d[i] <= 0) m[i]=0;
            else
            {
                double w1 = 2*(x[i+1]-x[i]) + (x[i]-x[i-1]);
                double w2 = (x[i+1]-x[i]) + 2*(x[i]-x[i-1]);
                m[i] = (w1 * d[i-1] + w2 * d[i]) / (w1 + w2);
                double a = m[i]/d[i-1];
                double b = m[i]/d[i];
                if (a*a + b*b > 9)
                {
                    double tau = 3/Math.Sqrt(a*a + b*b);
                    m[i] = tau * m[i];
                }
            }
        }
    }

    public double Eval(double t)
    {
        if (t<=x[0]) return y[0];
        if (t>=x[^1]) return y[^1];
        int i=Array.BinarySearch(x, t);
        if (i<0) i = ~i - 1;
        i = Math.Clamp(i, 0, x.Length-2);
        double h = x[i+1]-x[i];
        double s = (t - x[i]) / h;
        double h00 = (1 + 2*s)*(1 - s)*(1 - s);
        double h10 = s*(1 - s)*(1 - s);
        double h01 = s*s*(3 - 2*s);
        double h11 = s*s*(s - 1);
        return h00*y[i] + h*h10*m[i] + h01*y[i+1] + h*h11*m[i+1];
    }
}
