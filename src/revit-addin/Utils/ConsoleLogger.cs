using System;

namespace TycoonRevitAddin.Utils
{
    /// <summary>
    /// Simple console logger implementation for script execution
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        public void Log(string message)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        }

        public void LogError(string message, Exception exception = null)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ERROR: {message}");
            if (exception != null)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] EXCEPTION: {exception}");
            }
        }
    }
}
