// Interpolateur par spline cubique naturel appliqué aux zéro-taux
//
// Principe : on construit un spline S(t) tel que sur chaque intervalle [x_i,x_{i+1}]
// S est un polynôme cubique continu, avec des dérivées premières et secondes continues
// sur l'ensemble. Ce code suit la construction classique (système tridiagonal) pour
// obtenir les coefficients a,b,c,d qui définissent chaque polynôme local.
namespace RateCurveProject.Models.Interpolation;

using RateCurveProject.Models;

public class CubicSplineInterpolator : IInterpolator
{
    private double[] x = Array.Empty<double>();
    private double[] y = Array.Empty<double>();
    private double[] a = Array.Empty<double>();
    private double[] b = Array.Empty<double>();
    private double[] c = Array.Empty<double>();
    private double[] d = Array.Empty<double>();

    /// <summary>
    /// Calibre l'interpolateur à partir d'une liste de points (T, ZeroRate) triés
    /// (ou non — la méthode conserve l'ordre d'entrée pour construire les noeuds).
    /// </summary>
    public void Build(IReadOnlyList<CurvePoint> points)
    {
        x = points.Select(p => p.T).ToArray();
        y = points.Select(p => p.ZeroRate).ToArray();
        int n = x.Length - 1;
        a = new double[n];
        b = new double[n];
        c = new double[n+1];
        d = new double[n];

        double[] h = new double[n];
        for (int i = 0; i < n; i++) h[i] = x[i+1]-x[i];

        double[] alpha = new double[n];
        for (int i = 1; i < n; i++)
            alpha[i] = (3/h[i])*(y[i+1]-y[i]) - (3/h[i-1])*(y[i]-y[i-1]);

        double[] l = new double[n+1];
        double[] mu = new double[n+1];
        double[] z = new double[n+1];
        l[0]=1; mu[0]=0; z[0]=0;
        for (int i=1;i<n;i++)
        {
            l[i]=2*(x[i+1]-x[i-1]) - h[i-1]*mu[i-1];
            mu[i]=h[i]/l[i];
            z[i]=(alpha[i]-h[i-1]*z[i-1])/l[i];
        }
        l[n]=1; z[n]=0; c[n]=0;

        for (int j=n-1;j>=0;j--)
        {
            c[j]=z[j]-mu[j]*c[j+1];
            b[j]=(y[j+1]-y[j])/h[j] - h[j]*(c[j+1]+2*c[j])/3;
            d[j]=(c[j+1]-c[j])/(3*h[j]);
            a[j]=y[j];
        }
    }

    /// <summary>
    /// Évalue le zéro-taux interpolé à la maturité t en utilisant le spline construit.
    /// - Si t en dehors des bornes on renvoie respectivement la première ou dernière valeur.
    /// - Dans l'intervalle, on récupère l'indice de segment puis on calcule le polynôme cubique.
    /// </summary>
    public double Eval(double t)
    {
        if (t<=x[0]) return y[0];
        if (t>=x[^1]) return y[^1];
        int i=Array.BinarySearch(x, t);
        if (i<0) i = ~i - 1;
        i = Math.Clamp(i, 0, x.Length-2);
        double dx = t - x[i];
        return a[i] + b[i]*dx + c[i]*dx*dx + d[i]*dx*dx*dx;
    }
}
