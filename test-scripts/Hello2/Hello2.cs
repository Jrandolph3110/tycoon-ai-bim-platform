using System;
using Tycoon.Scripting.Contracts;

namespace Hello2Script
{
    /// <summary>
    /// Hello2 script to test Smart Tools panel placement and hot-reload functionality
    /// </summary>
    public class Hello2Script : IScript
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
                string message = $"🎉 Hello2 from Smart Tools Panel!\n\n" +
                               $"👤 User: {userName}\n" +
                               $"🕒 Time: {currentTime:yyyy-MM-dd HH:mm:ss}\n" +
                               $"📋 Selected Elements: {selectedCount}\n\n" +
                               $"🧠 This script is in the Smart Tools panel!\n" +
                               $"✅ Hot-reload functionality is working!\n" +
                               $"🚀 This script was dynamically loaded and executed.\n\n" +
                               $"This demonstrates panel placement and hot-reload system working correctly!";

                // Show message using the host interface
                host.ShowMessage("Hello2 - Smart Tools Test", message);

                // Log success to console
                Console.WriteLine($"[Hello2] Script executed successfully at {currentTime} for user {userName}");
                Console.WriteLine($"[Hello2] Selected elements: {selectedCount}");
            }
            catch (Exception ex)
            {
                string errorMessage = $"Hello2 script failed: {ex.Message}";
                Console.WriteLine($"[Hello2] Error: {errorMessage}");

                // Show error message
                host.ShowMessage("Hello2 - Error", $"Script execution failed:\n{errorMessage}");

                // Re-throw to let the host handle transaction rollback
                throw;
            }
        }
    }
}
