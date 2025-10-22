namespace RateCurveProject.Models.Interpolation;

using RateCurveProject.Models;

public class SmithWilsonInterpolator : IInterpolator
{
    private double[] t = Array.Empty<double>();
    private double[] p = Array.Empty<double>();
    private double[] q = Array.Empty<double>();
    private double ufr;
    private double lambda;

    public SmithWilsonInterpolator(double ultimateForwardRate, double lambda)
    {
        this.ufr = ultimateForwardRate;
        this.lambda = lambda;
    }

    public void Build(IReadOnlyList<CurvePoint> points)
    {
        t = points.Select(p => p.T).ToArray();
        p = points.Select(p => Math.Exp(-p.ZeroRate * p.T)).ToArray();
        int n = t.Length;
        double[,] K = new double[n, n];
        for (int i=0;i<n;i++)
            for (int j=0;j<n;j++)
                K[i,j] = WilsonKernel(t[i], t[j], lambda);
        double[] rhs = new double[n];
        for (int i=0;i<n;i++)
            rhs[i] = p[i] - Math.Exp(-ufr * t[i]);
        q = Solve(K, rhs);
    }

    public double Eval(double tau)
    {
        double baseDF = Math.Exp(-ufr * tau);
        double sum = 0.0;
        for (int i=0;i<t.Length;i++)
            sum += WilsonKernel(tau, t[i], lambda) * q[i];
        double DF = baseDF + sum;
        if (DF<=1e-12) DF = 1e-12;
        return tau>0 ? -Math.Log(DF)/tau : 0.0;
    }

    private static double WilsonKernel(double t, double s, double lambda)
    {
        double min = Math.Min(t, s);
        double max = Math.Max(t, s);
        double e1 = Math.Exp(-lambda * (t + s));
        double e2 = Math.Exp(-lambda * (max - min));
        return 0.5 * (lambda * min - e2) * e1;
    }

    private static double[] Solve(double[,] A, double[] b)
    {
        int n = b.Length;
        double[,] M = (double[,])A.Clone();
        double[] B = (double[])b.Clone();
        for (int k=0;k<n;k++)
        {
            int i_max=k;
            double maxv=Math.Abs(M[k,k]);
            for (int i=k+1;i<n;i++)
                if (Math.Abs(M[i,i])>maxv){ maxv=Math.Abs(M[i,i]); i_max=i; }
            if (i_max!=k)
            {
                for (int j=0;j<n;j++){ var tmp=M[k,j]; M[k,j]=M[i_max,j]; M[i_max,j]=tmp; }
                var tb=B[k]; B[k]=B[i_max]; B[i_max]=tb;
            }
            double piv = M[k,k];
            if (Math.Abs(piv)<1e-14) piv=1e-14;
            for (int i=k+1;i<n;i++)
            {
                double factor = M[i,k]/piv;
                B[i] -= factor*B[k];
                for (int j=k;j<n;j++) M[i,j]-=factor*M[k,j];
            }
        }
        var x = new double[n];
        for (int i=n-1;i>=0;i--)
        {
            double sum=B[i];
            for (int j=i+1;j<n;j++) sum -= M[i,j]*x[j];
            double piv=M[i,i]; if (Math.Abs(piv)<1e-14) piv=1e-14;
            x[i]=sum/piv;
        }
        return x;
    }
}
