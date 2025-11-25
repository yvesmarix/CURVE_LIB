using System.Globalization;
using System.Text;
using RateCurveProject.Engine;
using RateCurveProject.Models;

namespace RateCurveProject.Output;

/// <summary>
/// Gestionnaire d'export des données de courbe et des métriques au format CSV.
/// Regroupe les routines d'écriture de fichiers CSV et garantit l'utilisation
/// d'une culture indépendante pour la sérialisation numérique (CultureInfo.InvariantCulture).
/// </summary>
public class ExportManager
{
    private readonly string _dir;
    /// <summary>
    /// Crée un ExportManager qui écrira les fichiers dans le répertoire <paramref name="dir"/>.
    /// </summary>
    /// <param name="dir">Répertoire de sortie pour les fichiers CSV (sera créé ailleurs si besoin).</param>
    public ExportManager(string dir){ _dir = dir; }

    /// <summary>
    /// Exporte la courbe zéro-taux et quelques séries dérivées en un fichier CSV.
    /// Format des colonnes : T,Zero,DF,Forward
    /// - T : maturité (années)
    /// - Zero : zéro-taux continu Z(T) (fraction, ex: 0.03 pour 3%)
    /// - DF : discount factor DF(T) = exp(-Z(T) * T)
    /// - Forward : forward instantané approx.
    /// </summary>
    /// <param name="curve">Objet <see cref="Curve"/> à échantillonner.</param>
    /// <param name="fileName">Nom du fichier CSV à écrire (relatif au répertoire configuré).</param>
    /// <param name="tStart">Maturité de départ (inclus).</param>
    /// <param name="tEnd">Maturité de fin (inclus si la grille le permet).</param>
    /// <param name="step">Pas d'échantillonnage en années (par défaut 0.25).</param>
    /// <returns>Chemin complet du fichier écrit.</returns>
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

    /// <summary>
    /// Exporte les métriques calculées (résultat de <see cref="CurveAnalyzer"/>) au format CSV.
    /// En-tête : T,Zero,DF,FwdInst,Slope,Convexity
    /// Les valeurs numériques sont écrites avec <see cref="CultureInfo.InvariantCulture"/>
    /// pour assurer la portabilité (decimal point = '.').
    /// </summary>
    /// <param name="metrics">Collection des métriques à exporter.</param>
    /// <param name="fileName">Nom du fichier CSV cible (relatif au répertoire configuré).</param>
    /// <returns>Chemin complet du fichier écrit.</returns>
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
