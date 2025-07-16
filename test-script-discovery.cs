using System;
using System.IO;
using System.Reflection;

// Simple test program to verify script discovery functionality
class TestScriptDiscovery
{
    static void Main(string[] args)
    {
        Console.WriteLine("🔍 Testing Script Discovery Functionality");
        Console.WriteLine("==========================================");
        
        try
        {
            // Load the TycoonRevitAddin assembly
            var assemblyPath = @"C:\RevitAI\tycoon-ai-bim-platform\src\revit-addin\bin\Release\TycoonRevitAddin.dll";
            if (!File.Exists(assemblyPath))
            {
                Console.WriteLine($"❌ Assembly not found: {assemblyPath}");
                return;
            }
            
            var assembly = Assembly.LoadFrom(assemblyPath);
            Console.WriteLine($"✅ Loaded assembly: {assembly.FullName}");
            
            // Get the ScriptDiscoveryService type
            var discoveryServiceType = assembly.GetType("TycoonRevitAddin.Scripting.ScriptDiscoveryService");
            if (discoveryServiceType == null)
            {
                Console.WriteLine("❌ ScriptDiscoveryService type not found");
                return;
            }
            
            Console.WriteLine("✅ Found ScriptDiscoveryService type");
            
            // Create an instance
            var discoveryService = Activator.CreateInstance(discoveryServiceType);
            
            // Get the default script directory
            var getDefaultDirMethod = discoveryServiceType.GetMethod("GetDefaultScriptDirectory");
            var scriptDirectory = (string)getDefaultDirMethod.Invoke(null, null);
            
            Console.WriteLine($"📁 Default script directory: {scriptDirectory}");
            
            // Discover scripts
            var discoverMethod = discoveryServiceType.GetMethod("DiscoverScripts");
            var scripts = discoverMethod.Invoke(discoveryService, new object[] { scriptDirectory });
            
            // Get the count using reflection
            var scriptsType = scripts.GetType();
            var countProperty = scriptsType.GetProperty("Count");
            var scriptCount = (int)countProperty.GetValue(scripts);
            
            Console.WriteLine($"🎯 Discovered {scriptCount} scripts:");
            
            // Enumerate scripts
            var enumerator = ((System.Collections.IEnumerable)scripts).GetEnumerator();
            int index = 1;
            while (enumerator.MoveNext())
            {
                var script = enumerator.Current;
                var scriptType = script.GetType();
                
                var nameProperty = scriptType.GetProperty("Name");
                var descProperty = scriptType.GetProperty("Description");
                var authorProperty = scriptType.GetProperty("Author");
                var panelProperty = scriptType.GetProperty("Panel");
                
                var name = nameProperty?.GetValue(script)?.ToString() ?? "Unknown";
                var description = descProperty?.GetValue(script)?.ToString() ?? "No description";
                var author = authorProperty?.GetValue(script)?.ToString() ?? "Unknown";
                var panel = panelProperty?.GetValue(script)?.ToString() ?? "Unknown";
                
                Console.WriteLine($"  {index}. {name}");
                Console.WriteLine($"     Description: {description}");
                Console.WriteLine($"     Author: {author}");
                Console.WriteLine($"     Panel: {panel}");
                Console.WriteLine();
                
                index++;
            }
            
            Console.WriteLine("🎉 Script discovery test completed successfully!");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error during script discovery test: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
        
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}
