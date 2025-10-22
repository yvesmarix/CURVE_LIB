# RateCurveProject

Mini-projet C# (.NET 8) : **Construction et lissage de courbes de taux** avec méthodes **Linear, CubicSpline, HaganWest (monotone)** et **Smith-Wilson**.  
Visualisation via **ScottPlot** (PNG).

## Structure
- `Data/MarketDataLoader.cs` : import CSV
- `Engine/Bootstrapper.cs` : bootstrap mono-courbe (démo)
- `Models/Interpolation/*` : méthodes de lissage
- `UI/CurvePlotter.cs` : exports PNG
- `Output/ExportManager.cs` : exports CSV
- `src/Samples/instruments_sample.csv` : données d'exemple

## CSV (src/Samples/instruments_sample.csv)
```
Type,MaturityYears,Rate,DayCount,FixedFreq
Deposit,0.25,0.010,ACT/360,1
Deposit,0.5,0.011,ACT/360,1
Deposit,1,0.012,ACT/360,1
Swap,2,0.013,ACT/360,2
Swap,3,0.014,ACT/360,2
Swap,5,0.016,ACT/360,2
Swap,7,0.0175,ACT/360,2
Swap,10,0.019,ACT/360,2
Swap,20,0.021,ACT/360,2
Swap,30,0.022,ACT/360,2
```

## Build & Run
```bash
dotnet build
dotnet run --project src/RateCurveProject.csproj src/Samples/instruments_sample.csv HaganWest
# méthodes possibles: Linear | CubicSpline | HaganWest | SmithWilson
```

Sorties (CSV & PNG) dans `OutputRuns/`.

## Notes
- Bootstrap mono-courbe de démonstration (dépôts + swaps).
- Hagan-West implémenté via **monotone cubic Hermite (Fritsch-Carlson limiter)** pour éviter les oscillations.
- Smith-Wilson inclut UFR=2.5% et λ=0.1 par défaut.
