/**
 * Simple Logger Interface for Tycoon AI-BIM Platform
 */

using System;

namespace TycoonRevitAddin.Utils
{
    public interface ILogger
    {
        void Log(string message);
        void LogError(string message, Exception ex = null);
    }

    public class ConsoleLogger : ILogger
    {
        public void Log(string message)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        }

        public void LogError(string message, Exception ex = null)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ERROR: {message}");
            if (ex != null)
            {
                Console.WriteLine($"Exception: {ex.Message}");
            }
        }
    }
}
