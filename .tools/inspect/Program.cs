using System;
using System.Linq;
using System.Reflection;

class Program
{
    static void PrintType(string path, string typeName)
    {
        var asm = Assembly.LoadFrom(path);
        Console.WriteLine($"Loaded: {asm.FullName}\n");
        var t = asm.GetTypes().FirstOrDefault(x => x.Name == typeName || x.FullName?.EndsWith(typeName) == true);
        if (t == null) {
            Console.WriteLine($"Type {typeName} not found in {path}\n");
            return;
        }
        Console.WriteLine(t.FullName + "\nMethods:\");
        foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            Console.WriteLine("  " + m.Name + "(" + string.Join(',', m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ")");
        Console.WriteLine("\nProperties:");
        foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            Console.WriteLine("  " + p.PropertyType.Name + " " + p.Name);
    }

    static void Main(string[] args)
    {
        if (args.Length < 2) {
            Console.WriteLine("Usage: inspect <path-to-dll> <TypeName>");
            return;
        }
        PrintType(args[0], args[1]);
    }
}
