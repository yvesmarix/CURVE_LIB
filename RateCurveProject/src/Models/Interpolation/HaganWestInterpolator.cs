using System;
using System.Collections.Generic;
using System.Linq;
using RateCurveProject.Models;

namespace RateCurveProject.Models.Interpolation
{
    /// <summary>
    /// Interpolateur "Hagan-West style" :
    /// - On interpole les ZERO-RATES (Z(t)) en utilisant un spline cubique hermitien monotone
    ///   (algorithme de Fritsch-Carlson sur les pentes).
    /// - L'objectif est d'obtenir une courbe lisse, sans oscillations absurdes, et monotone
    ///   quand les données initiales le sont.
    ///
    /// ATTENTION :
    /// - On travaille sur les zéro-taux continus déjà bootstrappés (CurvePoint.ZeroRate).
    /// - On ne traite ici QUE l'interpolation. La construction de la courbe (bootstrap)
    ///   est faite ailleurs.
    /// </summary>
    public class HaganWestInterpolator : IInterpolator
    {
        // Abscisses : maturités T_i (en années)
        private double[] x = Array.Empty<double>();

        // Ordonnées : zéro-taux Z(T_i)
        private double[] y = Array.Empty<double>();

        // Pentes m_i = Z'(T_i) après filtrage monotone
        private double[] m = Array.Empty<double>();

        /// <summary>
        /// Construit l'interpolateur à partir d'une liste de points (T, Z(T)).
        /// Hypothèse : les points sont déjà triés par maturité.
        /// Si ce n'est pas le cas, on les retrie.
        /// </summary>
        public void Build(IReadOnlyList<CurvePoint> points)
        {
            if (points == null || points.Count == 0)
                throw new ArgumentException("HaganWestInterpolator.Build: la liste de points est vide.");

            if (points.Count == 1)
            {
                // Cas dégénéré : une seule maturité, on ne peut rien interpoler.
                // On fera de l'extrapolation plate sur ce point.
                x = new[] { points[0].T };
                y = new[] { points[0].ZeroRate };
                m = new[] { 0.0 };
                return;
            }

            // On s'assure que les points sont triés par maturité croissante.
            var ordered = points.OrderBy(p => p.T).ToArray();

            x = ordered.Select(p => p.T).ToArray();
            y = ordered.Select(p => p.ZeroRate).ToArray();

            int n = x.Length;

            // d[i] = pente "secante" entre (x_i, y_i) et (x_{i+1}, y_{i+1})
            //       = (y_{i+1} - y_i) / (x_{i+1} - x_i)
            var d = new double[n - 1];
            for (int i = 0; i < n - 1; i++)
            {
                double dx = x[i + 1] - x[i];
                if (dx <= 0.0)
                    throw new ArgumentException("HaganWestInterpolator.Build: les maturités doivent être strictement croissantes.");

                d[i] = (y[i + 1] - y[i]) / dx;
            }

            // m[i] = pente au noeud i (à calibrer pour obtenir une spline monotone)
            m = new double[n];

            // Pente aux extrémités : on prend la pente de la première / dernière sécante
            m[0] = d[0];
            m[n - 1] = d[n - 2];

            // Cas généraux (noeuds internes) : Fritsch-Carlson
            for (int i = 1; i < n - 1; i++)
            {
                // Si changement de signe entre d[i-1] et d[i], ou si l'une des deux est nulle,
                // la courbe risque de créer un "wiggle" (non-monotone).
                // Pour éviter ça : pente nulle à ce noeud => "plate" localement.
                if (d[i - 1] * d[i] <= 0.0)
                {
                    m[i] = 0.0;
                }
                else
                {
                    // Sinon, on fait une moyenne pondérée des deux pentes adjacentes.
                    // Formule de Fritsch-Carlson (weights basés sur les distances h_{i-1}, h_i).
                    double hPrev = x[i] - x[i - 1]; // h_{i-1}
                    double hNext = x[i + 1] - x[i];     // h_i

                    double w1 = 2.0 * hNext + hPrev;
                    double w2 = hNext + 2.0 * hPrev;

                    double diPrev = d[i - 1];
                    double di = d[i];

                    double mi = (w1 * diPrev + w2 * di) / (w1 + w2);

                    // On applique ensuite la "limitation" de Hagan-West / Fritsch-Carlson :
                    // si la pente est trop grande par rapport aux secantes,
                    // on la réduit pour préserver la monotonie (critère a^2 + b^2 <= 9).
                    double a = mi / diPrev;
                    double b = mi / di;

                    if (a * a + b * b > 9.0)
                    {
                        double tau = 3.0 / Math.Sqrt(a * a + b * b);
                        mi = tau * mi;
                    }

                    m[i] = mi;
                }
            }
        }

        /// <summary>
        /// Évalue le zéro-taux Z(t) interpolé à la maturité t.
        /// </summary>
        public double Eval(double t)
        {
            if (x.Length == 0)
                throw new InvalidOperationException("HaganWestInterpolator.Eval: l'interpolateur n'a pas été construit (Build non appelé).");

            // Cas dégénéré (un seul point) : extrapolation plate
            if (x.Length == 1)
                return y[0];

            // Extrapolation plate en dehors du domaine des données :
            // - pour t < x[0] : Z(t) = Z(x[0])
            // - pour t > x[n-1] : Z(t) = Z(x[n-1])
            if (t <= x[0]) return y[0];
            if (t >= x[^1]) return y[^1];

            // On cherche l'intervalle [x_i, x_{i+1}] qui contient t
            // BinarySearch renvoie :
            // - un indice >=0 si t == x[i],
            // - un complément à 1 si t n'est pas trouvé (~i - 1 = index de gauche).
            int i = Array.BinarySearch(x, t);
            if (i < 0)
                i = ~i - 1;

            // Sécurité : clamp pour rester dans [0, n-2]
            i = Math.Clamp(i, 0, x.Length - 2);

            double x0 = x[i];
            double x1 = x[i + 1];
            double y0 = y[i];
            double y1 = y[i + 1];
            double m0 = m[i];
            double m1 = m[i + 1];

            double h = x1 - x0;        // longueur de l'intervalle
            double s = (t - x0) / h;   // variable réduite s ∈ [0,1]

            // Base functions du spline cubique Hermite :
            // h00(s), h10(s), h01(s), h11(s)
            double s2 = s * s;
            double s3 = s2 * s;

            double h00 = (1 + 2 * s) * (1 - s) * (1 - s); // = 2s^3 - 3s^2 + 1
            double h10 = s * (1 - s) * (1 - s);           // = s^3 - 2s^2 + s
            double h01 = s2 * (3 - 2 * s);                // = -2s^3 + 3s^2
            double h11 = s2 * (s - 1);                    // = s^3 - s^2

            // Formule Hermite :
            // Z(t) = h00*y0 + h10*h*m0 + h01*y1 + h11*h*m1
            double zt = h00 * y0 + h * h10 * m0 + h01 * y1 + h * h11 * m1;

            return zt;
        }
    }
}
