using System;
using System.Collections.Generic;
using System.Linq;
using RateCurveProject.Models;

namespace RateCurveProject.Models.Interpolation
{
    /// <summary>
    /// Interpolateur / extrapolateur de courbe via la méthode de Smith-Wilson.
    ///
    /// Entrée : une liste de points (T, ZeroRate(T)) en taux continus.
    /// Sortie : Eval(tau) renvoie un zéro-taux continu Z(tau).
    ///
    /// Formule :
    ///   P(t) = exp(-UFR * t) + sum_j xi_j * W(t, u_j)
    ///   Z(t) = -ln(P(t)) / t
    ///
    /// Ici on est dans le cas simple : les "instruments" sont des zero-coupons
    /// déjà bootstrappés → on connaît P(u_j) = exp(-Z_j * u_j).
    /// </summary>
    public class SmithWilsonInterpolator : IInterpolator
    {
        // Maturités des piliers (u_j)
        private double[] pillarTimes = Array.Empty<double>();

        // Discount factors marché P(u_j)
        private double[] pillarDFs = Array.Empty<double>();

        // Coefficients xi_j (poids du noyau)
        private double[] xi = Array.Empty<double>();

        // Paramètres globaux
        private readonly double ufr;     // ultimate forward rate (continu)
        private readonly double lambda;  // vitesse de convergence (alpha)

        /// <summary>
        /// Constructeur.
        /// ufr    : ultimate forward rate (en taux continu, ex: 0.032 pour 3.2%)
        /// lambda : vitesse de reversion vers l'UFR (souvent autour de 0.1)
        /// </summary>
        public SmithWilsonInterpolator(double ultimateForwardRate, double lambda)
        {
            if (lambda <= 0.0)
                throw new ArgumentException("SmithWilsonInterpolator: lambda doit être > 0.", nameof(lambda));

            this.ufr = ultimateForwardRate;
            this.lambda = lambda;
        }

        /// <summary>
        /// Calibre l'interpolateur à partir des points (T, ZeroRate(T)).
        /// </summary>
        public void Build(IReadOnlyList<CurvePoint> points)
        {
            if (points == null || points.Count == 0)
                throw new ArgumentException("SmithWilsonInterpolator.Build: liste de points vide.");

            // 1) Tri par maturité
            var ordered = points.OrderBy(p => p.T).ToArray();

            pillarTimes = ordered.Select(p => p.T).ToArray();

            // Discount factor marché à chaque pilier :
            // P_i = exp( -Z_i * T_i )
            pillarDFs = ordered.Select(p => Math.Exp(-p.ZeroRate * p.T)).ToArray();

            int n = pillarTimes.Length;

            // 2) Matrice du noyau de Wilson K_ij = W(u_i, u_j)
            var K = new double[n, n];
            for (int i = 0; i < n; i++)
            {
                double ti = pillarTimes[i];
                for (int j = 0; j < n; j++)
                {
                    double tj = pillarTimes[j];
                    K[i, j] = WilsonKernel(ti, tj, lambda, ufr);
                }
            }

            // 3) Second membre : rhs_i = P_i - mu_i
            //    où mu_i = exp(-UFR * u_i) est le DF pur UFR
            var rhs = new double[n];
            for (int i = 0; i < n; i++)
            {
                double ti = pillarTimes[i];
                double mu = Math.Exp(-ufr * ti);
                rhs[i] = pillarDFs[i] - mu;
            }

            // 4) Résolution K * xi = rhs
            xi = SolveLinearSystem(K, rhs);
        }

        /// <summary>
        /// Renvoie le zéro-taux continu Z(tau) interpolé/extrapolé.
        /// </summary>
        public double Eval(double tau)
        {
            // Par convention : Z(0) = 0
            if (tau <= 0.0)
                return 0.0;

            if (pillarTimes.Length == 0 || xi.Length == 0)
                throw new InvalidOperationException("SmithWilsonInterpolator.Eval: Build n'a pas été appelé.");

            // ⚠️ Smith-Wilson n'est pas destiné à reconstituer le tout début de courbe.
            // Pour éviter les énormes niveaux proches de 0 (division par un tau minuscule),
            // on "clamp" les très petites maturités au premier pilier.
            //
            // → sinon, une erreur de 1bp sur le DF à 1 jour explose en centaines de % de taux.
            double tMin = pillarTimes[0];
            if (tau < tMin)
            {
                // on renvoie simplement le zéro-taux du premier pilier
                return -Math.Log(pillarDFs[0]) / tMin;
            }

            // 1) DF de base : exp(-UFR * tau)
            double baseDF = Math.Exp(-ufr * tau);

            // 2) Contribution Smith-Wilson : Σ xi_j * W(tau, u_j)
            double sum = 0.0;
            for (int j = 0; j < pillarTimes.Length; j++)
            {
                double uj = pillarTimes[j];
                sum += WilsonKernel(tau, uj, lambda, ufr) * xi[j];
            }

            double DF = baseDF + sum;

            // Sécurité numérique : DF ne doit pas être <= 0
            if (DF < 1e-10)
                DF = 1e-10;

            // 3) Conversion DF → zéro-taux continu
            double zero = -Math.Log(DF) / tau;
            return zero;
        }

        /// <summary>
        /// Noyau de Wilson W(t, u) conforme à la définition Smith-Wilson / EIOPA :
        ///
        ///   W(t,u) = exp(-UFR * (t+u)) *
        ///            [ alpha * min(t,u)
        ///              - 0.5 * exp(-alpha * max(t,u))
        ///                    * (exp(alpha * min(t,u)) - exp(-alpha * min(t,u)) ) ]
        ///
        /// t, u en années, alpha = lambda.
        /// </summary>
        private static double WilsonKernel(double t, double u, double alpha, double ufr)
        {
            double m = Math.Min(t, u);
            double M = Math.Max(t, u);

            // Terme entre crochets [...]
            double part1 = alpha * m;
            double part2 = 0.5 * Math.Exp(-alpha * M)
                                 * (Math.Exp(alpha * m) - Math.Exp(-alpha * m));
            double inner = part1 - part2;

            // Facteur exp(-UFR * (t+u))
            double expUfr = Math.Exp(-ufr * (t + u));

            return expUfr * inner;
        }

        /// <summary>
        /// Résolution naïve du système linéaire A x = b par élimination de Gauss
        /// avec pivot partiel.
        /// Suffisant pour n ~ 30-50 piliers.
        /// </summary>
        private static double[] SolveLinearSystem(double[,] A, double[] b)
        {
            int n = b.Length;
            var M = (double[,])A.Clone();
            var B = (double[])b.Clone();

            // Descente (factorisation)
            for (int k = 0; k < n; k++)
            {
                // Recherche du pivot max en colonne k
                int iMax = k;
                double maxVal = Math.Abs(M[k, k]);
                for (int i = k + 1; i < n; i++)
                {
                    double val = Math.Abs(M[i, k]);  // <-- pivot sur la colonne k
                    if (val > maxVal)
                    {
                        maxVal = val;
                        iMax = i;
                    }
                }

                // Échange de lignes si besoin
                if (iMax != k)
                {
                    for (int j = 0; j < n; j++)
                    {
                        double tmp = M[k, j];
                        M[k, j] = M[iMax, j];
                        M[iMax, j] = tmp;
                    }

                    double tb = B[k];
                    B[k] = B[iMax];
                    B[iMax] = tb;
                }

                double piv = M[k, k];
                if (Math.Abs(piv) < 1e-14)
                    piv = Math.CopySign(1e-14, piv);

                // Élimination sur les lignes i > k
                for (int i = k + 1; i < n; i++)
                {
                    double factor = M[i, k] / piv;

                    B[i] -= factor * B[k];
                    for (int j = k; j < n; j++)
                        M[i, j] -= factor * M[k, j];
                }
            }

            // Remontée (back-substitution)
            var x = new double[n];
            for (int i = n - 1; i >= 0; i--)
            {
                double sum = B[i];
                for (int j = i + 1; j < n; j++)
                    sum -= M[i, j] * x[j];

                double piv = M[i, i];
                if (Math.Abs(piv) < 1e-14)
                    piv = Math.CopySign(1e-14, piv);

                x[i] = sum / piv;
            }

            return x;
        }
    }
}
