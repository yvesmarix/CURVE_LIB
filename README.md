# Projet « RateCurveProject »

**Construction et lissage de courbes de taux multi-méthodes**

-----

## 1\. Objectif du projet

Le projet **RateCurveProject** implémente une chaîne complète de construction de **courbes de taux zéro-coupon** à partir de données d'obligations d'états (initialement des swaps et dépôts avaient été envisagés, mais les données utilisées sont des obligations).

Une fois la courbe zéro-coupon **bootstrappée**, le projet applique plusieurs **méthodes d’interpolation et de lissage** pour obtenir une structure de taux continue. Il calcule ensuite un ensemble de **métriques de courbe** (taux zéro, facteurs d’actualisation, forwards instantanés, pente, convexité) et génère des visualisations statiques (PNG) et interactives (HTML/Plotly).

L’ensemble est écrit en **C\# / .NET** et structuré en modules fortement typés, mettant l'accent sur :

  * La **séparation des responsabilités** (chargement des données, moteur de calcul, visualisation, export).
  * La **qualité visuelle** (courbes annotées, exports HTML multi-graphiques).
  * La **robustesse numérique** (algorithmes de bootstrap, interpolation monotone, Smith–Wilson).

-----

## 2\. Architecture globale

Le projet est organisé en plusieurs **namespaces** :

  * `RateCurveProject.Data`: Chargement des instruments de marché (Excel) et calcul des rendements obligataires.
  * `RateCurveProject.Models`: Représentation de la courbe (`Curve`, `CurvePoint`) et stratégies d’interpolation (`IInterpolator` + implémentations).
  * `RateCurveProject.Engine`: Bootstrap de la courbe zéro-coupon et analyse (métriques dérivées).
  * `RateCurveProject.UI`: Génération de graphiques PNG via ScottPlot.
  * `RateCurveProject.Output`: Exports CSV et HTML/Plotly de haute qualité.
  * `RateCurveProject.Tests`: Tests unitaires (interpolation, analyse, exports, cohérence de la courbe).

Le point d’entrée `Program` orchestre le pipeline complet :

1.  Résolution du répertoire racine du projet (`FindProjectRoot`).
2.  Chargement des instruments via `MarketDataLoader` (fichier Excel par pays).
3.  Affichage des instruments bruts dans la console (tableau formaté `ConsoleTables`).
4.  Bootstrap des zéro-taux via `Bootstrapper`.
5.  Construction de la courbe `Curve` avec différentes méthodes d’interpolation:
      * `LinearInterpolator`
      * `CubicSplineInterpolator`
      * `HaganWestInterpolator`
      * `SmithWilsonInterpolator`
6.  Analyse de la courbe via `CurveAnalyzer`.
7.  Export CSV + HTML via `ExportManager`.
8.  Visualisation PNG + HTML interactifs via `CurvePlotter`.

Structure des exports :

```
OutputRuns/
  France/
    Linear/
      curve_plot_Linear.png
      forward_plot_Linear.png
      courbe_zero_interactive_Linear.html
      ...
    HaganWest/
    SmithWilson/
  US/
    ...
```

-----

## 3\. Données de marché et pré-traitement (`RateCurveProject.Data`)

### 3.1 Représentation des instruments

```csharp
public enum InstrumentType
{
    Deposit,
    SWAP,
    BOND
}

public class MarketInstrument
{
    public InstrumentType Type { get; set; }
    public double MaturityYears { get; set; }
    public double Rate { get; set; }       // taux du dépôt/swap, ou yield de l’obligation
    public double Coupon { get; set; }     // coupon en %
    public int FixedFreq { get; set; }     // fréquence de paiement fixe (1 = annuel, 0 = zéro-coupon)
    public double Price { get; set; }      // prix en fraction du nominal
}
```

Les maturités sont exprimées en **années fractionnelles**.

### 3.2 Chargement depuis Excel : `MarketDataLoader`

`MarketDataLoader` s’appuie sur **ClosedXML** pour :

  * Ouvrir un classeur Excel de cotations.
  * Lire les lignes instrument par instrument (type, maturité, taux, coupon, prix).
  * Interpréter certaines obligations comme zéro-coupon via un booléen `treatAsZeroCoupon`.
  * Construire une liste de `MarketInstrument` nettoyée (tri par maturité, suppression de doublons).

### 3.3 Calcul de rendement obligataire : `BondYieldCalculator`

```csharp
public static class BondYieldCalculator
{
    public static double ComputeYield(double couponPct, double maturityYears, double pricePct)
    { ... }
}
```

**Cas zéro-coupon** :
$$y = P^{-\frac{1}{T}} - 1$$

**Cas couponné** : Le prix $P$ est donné par
$$P = \sum_{k=1}^{n} \frac{c}{(1+y)^k} + \frac{1}{(1+y)^n},$$
et le yield $y$ est obtenu numériquement (dichotomie robuste).

-----

## 4\. Bootstrap de la courbe zéro-coupon (`RateCurveProject.Engine.Bootstrapper`)

```csharp
public record CurvePoint(double T, double ZeroRate);
```

### 4.1 Hypothèses

  * **Nominal** fixé à 1.
  * **Coupons** des obligations payés à fréquence `FixedFreq`.
  * `MarketInstrument.Price`: prix en fraction du nominal.
  * `MarketInstrument.Rate`:
      * Taux simple pour les dépôts.
      * Taux fixe des swaps.
      * Yield pour les obligations.

### 4.2 Logique conceptuelle

**Dépôts (partie courte)** :
$$DF(T) = \frac{1}{1 + rT}, \qquad Z(T) \approx \frac{1}{T}\,\ln\!\left(\frac{1}{DF(T)}\right).$$

**Obligations à coupon (partie longue)** :
$$P = \sum_i \text{coupon}_i \cdot DF(t_i) + DF(T),$$
d’où
$$DF(T) = \frac{P - A}{B}, \qquad Z_{\text{bond}}(T) = -\frac{\ln DF(T)}{T}.$$

-----

## 5\. Représentation de la courbe (`RateCurveProject.Models.Curve`)

```csharp
public class Curve
{
    private readonly List<CurvePoint> _points;
    private readonly IInterpolator _interp;

    public Curve(IEnumerable<CurvePoint> points, IInterpolator interpolator)
    {
        _points = points.OrderBy(p => p.T).ToList();
        _interp = interpolator;
        _interp.Build(_points);
    }

    public double Zero(double t) => _interp.Eval(t);

    public double DF(double t) => Math.Exp(-Zero(t) * t);

    public double ForwardInstantaneous(double t, double h = 1e-4)
    {
        var p1 = DF(Math.Max(t - h, 1e-6));
        var p2 = DF(t + h);
        return -(Math.Log(p2) - Math.Log(p1)) / (2*h);
    }

    public IReadOnlyList<CurvePoint> RawPoints => _points;
}
```

**Conversion zéro-taux / facteur d’actualisation ($DF$)** :
$$DF(t) = e^{-Z(t)\,t}.$$

**Forward instantané ($f(t)$)** (approximation par différences finies centrées) :
$$f(t) \approx -\frac{\ln DF(t+h) - \ln DF(t-h)}{2h}.$$

-----

## 6\. Méthodes d’interpolation et de lissage

```csharp
public interface IInterpolator
{
    void Build(IReadOnlyList<CurvePoint> points);
    double Eval(double t);
}
```

### 6.1 Interpolation linéaire

$$Z(t) = (1-w)\,Z_a + w\,Z_b, \qquad w = \frac{t - T_a}{T_b - T_a}.$$

### 6.2 Spline cubique naturel

On construit un spline cubique naturel $S(t)$ tel que $S$, $S'$, $S''$ soient continus aux points de jonction et $S''$ nul aux bornes.

### 6.3 Interpolation de type Hagan–West

On utilise un **spline de Hermite monotone**:
$$Z(t) = h_{00}(s)\,y_0 + h_{10}(s)\,h\,m_0 + h_{01}(s)\,y_1 + h_{11}(s)\,h\,m_1,$$
avec
$$s = \frac{t - T_i}{T_{i+1}-T_i}.$$

### 6.4 Méthode de Smith–Wilson

Pour chaque pilier $(u_j, Z(u_j))$, on définit $P(u_j) = e^{-Z(u_j)\,u_j}$.

La courbe Smith–Wilson est définie par:
$$P(t) = e^{-\text{UFR}\,t} + \sum_j \xi_j\,W(t, u_j), \qquad Z(t) = -\frac{\ln P(t)}{t},$$
où $W(t,u_j)$ est le noyau Smith–Wilson, $\text{UFR}$ l’**ultimate forward rate** et $\lambda$ le paramètre de convergence.

-----

## 7\. Analyse de la courbe (`CurveAnalyzer`)

```csharp
public record Metric(
    double T,
    double Zero,
    double DF,
    double Forward,
    double Slope,
    double SecondDerivative
);
```

Pour un grillage de maturités $\{T_k\}$, on calcule $Z(T_k)$, $DF(T_k)$, le forward $f(T_k)$, la **pente** $Z'(T_k)$ et la **convexité** $Z''(T_k)$ (par différences finies).

-----

## 8\. Exports et visualisation

### 8.1 Export CSV et HTML

  * **CSV** : colonnes $T$, $Zero$, $DF$, $Forward$.
  * **HTML Plotly** : courbes interactives (zéros, $DF$, forwards, dérivées).

### 8.2 Visualisation ScottPlot

  * Courbe zéro-taux + discount factors.
  * Courbe de forwards instantanés.
  * Colorisation et légendes par méthode (Linear, CubicSpline, HaganWest, SmithWilson).

-----

## 9\. Orchestration (`Program`)

Pour chaque méthode d’interpolation, le programme construit une `Curve`, calcule les métriques, exporte CSV/HTML et génère les PNG correspondants.

-----

## 10\. Pistes d’extension quantitative

  * Conventions de marché complètes (day count, calendriers, business day adjustment).
  * Courbes multi-devises / multi-courbes (OIS vs courbe de projection).
  * Bucket sensitivities par pilier, grecs de courbe.
  * Diagnostics visuels avancés (heatmaps de forwards, contributions instrument par instrument).

Le projet **RateCurveProject** fournit ainsi un socle robuste de construction et d’analyse de courbes de taux pour des usages de pricing, de gestion du risque de taux et d’ALM.