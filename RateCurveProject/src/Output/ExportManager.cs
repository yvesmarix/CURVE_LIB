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

    /// <summary>
    /// Exporte les métriques calculées au format HTML interactif avec graphiques Plotly.
    /// Crée un tableau interactif et des visualisations des dérivées (slope, convexité).
    /// Les taux sont affichés en pourcentages avec 4 décimales.
    /// </summary>
    /// <param name="metrics">Collection des métriques à exporter.</param>
    /// <param name="fileName">Nom du fichier HTML cible (relatif au répertoire configuré).</param>
    /// <returns>Chemin complet du fichier écrit.</returns>
    public string ExportMetricsHtml(IEnumerable<CurveAnalyzer.Metric> metrics, string fileName)
    {
        var path = Path.Combine(_dir, fileName);
        var metricsList = metrics.ToList();

        // Préparer les données pour les graphiques
        var ts = metricsList.Select(m => m.T).ToList();
        var slopes = metricsList.Select(m => m.Slope).ToList();
        var convexities = metricsList.Select(m => m.Convexity).ToList();

        // Fonction helper pour sérialiser en array JavaScript
        string JsArray(IEnumerable<double> arr)
            => "[" + string.Join(",", arr.Select(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture))) + "]";

        string xsJs = JsArray(ts);
        string slopesJs = JsArray(slopes);
        string convexitiesJs = JsArray(convexities);

        // Générer le HTML avec Plotly.js
        string html = $@"<!DOCTYPE html>
<html lang=""fr"">
<head>
    <meta charset=""utf-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Métriques de Courbe de Taux</title>
    <script src=""https://cdn.plot.ly/plotly-2.30.0.min.js""></script>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background: #f5f5f5;
            padding: 20px;
            color: #333;
        }}
        .container {{
            max-width: 1400px;
            margin: 0 auto;
        }}
        .header {{
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 40px;
            border-radius: 8px;
            margin-bottom: 30px;
            box-shadow: 0 10px 30px rgba(0,0,0,0.2);
        }}
        .header h1 {{
            font-size: 32px;
            margin-bottom: 10px;
            font-weight: 300;
        }}
        .header p {{
            font-size: 14px;
            opacity: 0.9;
        }}
        .charts-grid {{
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 20px;
            margin-bottom: 30px;
        }}
        .chart-container {{
            background: white;
            border-radius: 8px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
            padding: 20px;
        }}
        .chart-container h3 {{
            font-size: 16px;
            margin-bottom: 15px;
            color: #667eea;
            font-weight: 600;
        }}
        .chart {{
            width: 100%;
            height: 350px;
        }}
        .table-container {{
            background: white;
            border-radius: 8px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
            overflow: hidden;
            margin-bottom: 30px;
        }}
        table {{
            width: 100%;
            border-collapse: collapse;
        }}
        thead {{
            background: #f8f9fa;
            border-bottom: 2px solid #667eea;
        }}
        th {{
            padding: 16px;
            text-align: left;
            font-weight: 600;
            color: #333;
            font-size: 13px;
            text-transform: uppercase;
            letter-spacing: 0.5px;
        }}
        td {{
            padding: 12px 16px;
            border-bottom: 1px solid #e0e0e0;
            font-size: 13px;
        }}
        tbody tr:hover {{
            background-color: #f9f9f9;
        }}
        tbody tr:nth-child(even) {{
            background-color: #fafafa;
        }}
        .metric {{
            font-weight: 500;
            color: #667eea;
        }}
        .positive {{ color: #27ae60; }}
        .negative {{ color: #e74c3c; }}
        .footer {{
            text-align: center;
            padding: 20px;
            color: #999;
            font-size: 12px;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>📊 Métriques de Courbe de Taux</h1>
            <p>Analyse interactive des taux zéro, facteurs d'actualisation et dérivées</p>
        </div>

        <div class=""charts-grid"">
            <div class=""chart-container"">
                <h3>Slope (dZ/dt)</h3>
                <div id=""chartSlope"" class=""chart""></div>
            </div>
            <div class=""chart-container"">
                <h3>Convexité (d²Z/dt²)</h3>
                <div id=""chartConvexity"" class=""chart""></div>
            </div>
        </div>

        <div class=""table-container"">
            <table>
                <thead>
                    <tr>
                        <th>Maturité (ans)</th>
                        <th>Taux Zéro (%)</th>
                        <th>Discount Factor</th>
                        <th>Forward (%)</th>
                        <th>Slope</th>
                        <th>Convexité</th>
                    </tr>
                </thead>
                <tbody>";

        // Ajouter les lignes du tableau
        foreach (var m in metricsList)
        {
            var zeroPercent = (m.Zero * 100).ToString("F4", CultureInfo.InvariantCulture);
            var fwdPercent = (m.FwdInst * 100).ToString("F4", CultureInfo.InvariantCulture);
            var dfValue = m.DF.ToString("F6", CultureInfo.InvariantCulture);
            var slopeClass = m.Slope >= 0 ? "positive" : "negative";
            var convexityClass = m.Convexity >= 0 ? "positive" : "negative";
            var slopeValue = m.Slope.ToString("F6", CultureInfo.InvariantCulture);
            var convexityValue = m.Convexity.ToString("F6", CultureInfo.InvariantCulture);
            var tValue = m.T.ToString("F2", CultureInfo.InvariantCulture);

            html += $@"
                    <tr>
                        <td class=""metric"">{tValue}</td>
                        <td class=""metric"">{zeroPercent}</td>
                        <td>{dfValue}</td>
                        <td class=""metric"">{fwdPercent}</td>
                        <td class=""{slopeClass}"">{slopeValue}</td>
                        <td class=""{convexityClass}"">{convexityValue}</td>
                    </tr>";
        }

        html += $@"
                </tbody>
            </table>
        </div>

        <div class=""footer"">
            <p>Généré le {DateTime.Now:yyyy-MM-dd HH:mm:ss} | RateCurveProject</p>
        </div>
    </div>

    <script>
        const xs = {xsJs};
        const slopes = {slopesJs};
        const convexities = {convexitiesJs};

        // Graphique 1: Slope
        const traceSlope = {{
            x: xs,
            y: slopes,
            mode: 'lines+markers',
            name: 'Slope (dZ/dt)',
            line: {{ color: '#e74c3c', width: 2 }},
            marker: {{ size: 4 }}
        }};
        const layoutSlope = {{
            title: '',
            xaxis: {{ title: 'Maturité (années)' }},
            yaxis: {{ title: 'Slope' }},
            margin: {{ t: 20, l: 60, r: 60, b: 50 }},
            hovermode: 'x unified'
        }};
        Plotly.newPlot('chartSlope', [traceSlope], layoutSlope, {{ responsive: true }});

        // Graphique 2: Convexité
        const traceConvexity = {{
            x: xs,
            y: convexities,
            mode: 'lines+markers',
            name: 'Convexité (d²Z/dt²)',
            line: {{ color: '#f39c12', width: 2 }},
            marker: {{ size: 4 }}
        }};
        const layoutConvexity = {{
            title: '',
            xaxis: {{ title: 'Maturité (années)' }},
            yaxis: {{ title: 'Convexité' }},
            margin: {{ t: 20, l: 60, r: 60, b: 50 }},
            hovermode: 'x unified'
        }};
        Plotly.newPlot('chartConvexity', [traceConvexity], layoutConvexity, {{ responsive: true }});
    </script>
</body>
</html>";

        using var sw = new StreamWriter(path, false, Encoding.UTF8);
        sw.Write(html);
        sw.Flush();

        return path;
    }
}
