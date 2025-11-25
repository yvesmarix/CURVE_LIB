using System;
using System.Linq;
using System.Reflection;

class Program
{
    static void Main()
    {
        // attempt to inspect ScottPlot Plot type
        var plotPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\.nuget\\packages\\scottplot\\5.0.18\\lib\\net8.0\\ScottPlot.dll";
        Console.WriteLine("Inspecting: " + plotPath);
        var asm = Assembly.LoadFrom(plotPath);
        var types = asm.GetTypes().Select(t => t.FullName).OrderBy(x => x).ToArray();
        Console.WriteLine("Types in assembly:\n");
        foreach (var tn in types) Console.WriteLine(tn);

        // Plot methods (same assembly already loaded above)
        Console.WriteLine("\nInspecting Plot type from same assembly\n");
        var asmPlot = asm;
        var tPlot = asmPlot.GetType("ScottPlot.Plot");
        if (tPlot == null) { Console.WriteLine("Plot type not found"); return; }
        var plotMethods = tPlot.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name).Distinct().OrderBy(n => n).ToArray();
        Console.WriteLine("Plot public instance methods:\n");
        foreach (var m in plotMethods) Console.WriteLine(m);

            // Now inspect ScottPlot.WinForms.FormsPlot (if present)
            var winFormsPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\.nuget\\packages\\scottplot.winforms\\5.0.18\\lib\\net6.0-windows7.0\\ScottPlot.WinForms.dll";
            Console.WriteLine("\nInspecting WinForms assembly: " + winFormsPath);
            try
            {
                var asmWin = Assembly.LoadFrom(winFormsPath);
                var tForms = asmWin.GetTypes().FirstOrDefault(x => x.Name.IndexOf("FormsPlot", StringComparison.OrdinalIgnoreCase) >= 0);
                if (tForms == null) Console.WriteLine("FormsPlot-like type not found in WinForms assembly");
                else
                {
                    Console.WriteLine("Found: " + tForms.FullName);
                    var mf = tForms.GetMethods(BindingFlags.Public | BindingFlags.Instance).Select(m => m.ToString()).OrderBy(s => s).ToArray();
                    Console.WriteLine("FormsPlot methods:\n");
                    foreach (var s in mf) Console.WriteLine(s);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to load WinForms assembly: " + ex.Message);
            }
            Console.WriteLine("\nDetailed info for GetCoordinates method(s):\n");
            var getCoords = tPlot.GetMethods().Where(m => m.Name == "GetCoordinates");
            foreach (var m in getCoords)
            {
                Console.WriteLine(m.ToString());
                foreach (var p in m.GetParameters())
                    Console.WriteLine("   param: " + p.ParameterType.FullName + " " + p.Name);
                Console.WriteLine();
            }
                // Inspect Pixel constructors
                var pixelType = tPlot.Assembly.GetType("ScottPlot.Pixel");
                if (pixelType != null)
                {
                    Console.WriteLine("Pixel constructors:");
                    foreach (var c in pixelType.GetConstructors())
                    {
                        Console.WriteLine("  " + c.ToString());
                    }
                }
                    // Inspect Coordinates properties
                    var coordType = tPlot.Assembly.GetType("ScottPlot.Coordinates");
                    if (coordType != null)
                    {
                        Console.WriteLine("\nCoordinates members:");
                        foreach (var p in coordType.GetProperties()) Console.WriteLine("  " + p.Name + ": " + p.PropertyType.FullName);
                    }
    }
}
