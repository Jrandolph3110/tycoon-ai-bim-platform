using System;
using Tycoon.Scripting.Contracts;

namespace HelloWorldScript
{
    /// <summary>
    /// Simple Hello World script to test hot-reload functionality
    /// </summary>
    public class HelloWorldScript : IScript
    {
        public void Execute(IRevitHost host)
        {
            try
            {
                // Get current user info
                string userName = Environment.UserName;
                DateTime currentTime = DateTime.Now;

                // Get selected elements to show some interaction
                var selectedElements = host.GetSelectedElements();
                int selectedCount = selectedElements?.Count ?? 0;

                // Create a friendly message
                string message = $"🎉 Hello World from Tycoon AI-BIM!\n\n" +
                               $"👤 User: {userName}\n" +
                               $"🕒 Time: {currentTime:yyyy-MM-dd HH:mm:ss}\n" +
                               $"📋 Selected Elements: {selectedCount}\n\n" +
                               $"✅ Hot-reload functionality is working!\n" +
                               $"🚀 This script was dynamically loaded and executed.\n\n" +
                               $"This demonstrates that the registry-based script execution system is working correctly!";

                // Show message using the host interface
                host.ShowMessage("Hello World - Hot-Reload Test", message);

                // Log success to console
                Console.WriteLine($"[HelloWorld] Script executed successfully at {currentTime} for user {userName}");
                Console.WriteLine($"[HelloWorld] Selected elements: {selectedCount}");
            }
            catch (Exception ex)
            {
                string errorMessage = $"Hello World script failed: {ex.Message}";
                Console.WriteLine($"[HelloWorld] Error: {errorMessage}");

                // Show error message
                host.ShowMessage("Hello World - Error", $"Script execution failed:\n{errorMessage}");

                // Re-throw to let the host handle transaction rollback
                throw;
            }
        }
    }
}
