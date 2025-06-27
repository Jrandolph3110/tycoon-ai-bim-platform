using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;
using TycoonRevitAddin.Communication;

namespace TycoonRevitAddin.Resources
{
    /// <summary>
    /// Manages dynamic status icons for the Revit ribbon button
    /// </summary>
    public static class StatusIconManager
    {
        private static readonly string _tempIconPath = Path.Combine(Path.GetTempPath(), "TycoonStatusIcons");

        static StatusIconManager()
        {
            // Ensure temp directory exists
            Directory.CreateDirectory(_tempIconPath);
        }

        /// <summary>
        /// Get the appropriate icon path for the current connection status
        /// </summary>
        public static string GetStatusIconPath(ConnectionStatus status)
        {
            var iconFileName = $"tycoon-status-{status.ToString().ToLower()}.png";
            var iconPath = Path.Combine(_tempIconPath, iconFileName);

            // Generate icon if it doesn't exist
            if (!File.Exists(iconPath))
            {
                GenerateStatusIcon(status, iconPath);
            }

            return iconPath;
        }

        /// <summary>
        /// Generate a status icon with the appropriate color
        /// </summary>
        private static void GenerateStatusIcon(ConnectionStatus status, string outputPath)
        {
            const int size = 32;
            using (var bitmap = new Bitmap(size, size))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);

                // Get status color
                var statusColor = GetStatusColor(status);
                
                // Draw base circle (connection indicator)
                var baseRect = new Rectangle(2, 2, size - 4, size - 4);
                using (var baseBrush = new SolidBrush(Color.FromArgb(200, Color.DarkGray)))
                {
                    graphics.FillEllipse(baseBrush, baseRect);
                }

                // Draw status indicator
                var statusRect = new Rectangle(6, 6, size - 12, size - 12);
                using (var statusBrush = new SolidBrush(statusColor))
                {
                    graphics.FillEllipse(statusBrush, statusRect);
                }

                // Add inner highlight for better visibility
                var highlightRect = new Rectangle(8, 8, size - 16, size - 16);
                using (var highlightBrush = new SolidBrush(Color.FromArgb(100, Color.White)))
                {
                    graphics.FillEllipse(highlightBrush, highlightRect);
                }

                // Add status-specific indicators
                DrawStatusIndicator(graphics, status, size);

                // Save the icon
                bitmap.Save(outputPath, ImageFormat.Png);
            }
        }

        /// <summary>
        /// Draw status-specific indicators on the icon
        /// </summary>
        private static void DrawStatusIndicator(Graphics graphics, ConnectionStatus status, int size)
        {
            using (var pen = new Pen(Color.White, 2))
            {
                switch (status)
                {
                    case ConnectionStatus.Disconnected:
                        // Draw X for disconnected
                        graphics.DrawLine(pen, size / 4, size / 4, 3 * size / 4, 3 * size / 4);
                        graphics.DrawLine(pen, 3 * size / 4, size / 4, size / 4, 3 * size / 4);
                        break;

                    case ConnectionStatus.Available:
                        // Draw question mark for available but not connected
                        using (var font = new Font("Arial", size / 4, FontStyle.Bold))
                        using (var brush = new SolidBrush(Color.White))
                        {
                            var textRect = new RectangleF(0, 0, size, size);
                            var format = new StringFormat
                            {
                                Alignment = StringAlignment.Center,
                                LineAlignment = StringAlignment.Center
                            };
                            graphics.DrawString("?", font, brush, textRect, format);
                        }
                        break;

                    case ConnectionStatus.Connecting:
                        // Draw rotating indicator for connecting
                        var centerX = size / 2;
                        var centerY = size / 2;
                        var radius = size / 6;
                        
                        for (int i = 0; i < 8; i++)
                        {
                            var angle = i * Math.PI / 4;
                            var alpha = (int)(255 * (i + 1) / 8.0);
                            using (var connectingPen = new Pen(Color.FromArgb(alpha, Color.White), 2))
                            {
                                var x1 = centerX + (int)(radius * Math.Cos(angle));
                                var y1 = centerY + (int)(radius * Math.Sin(angle));
                                var x2 = centerX + (int)((radius + 4) * Math.Cos(angle));
                                var y2 = centerY + (int)((radius + 4) * Math.Sin(angle));
                                graphics.DrawLine(connectingPen, x1, y1, x2, y2);
                            }
                        }
                        break;

                    case ConnectionStatus.Connected:
                        // Draw checkmark for connected
                        var checkPoints = new Point[]
                        {
                            new Point(size / 4, size / 2),
                            new Point(size / 2 - 2, 3 * size / 4 - 2),
                            new Point(3 * size / 4, size / 4)
                        };
                        graphics.DrawLines(pen, checkPoints);
                        break;

                    case ConnectionStatus.Active:
                        // Draw pulse indicator for active
                        var pulseRect = new Rectangle(size / 4, size / 4, size / 2, size / 2);
                        using (var pulseBrush = new SolidBrush(Color.FromArgb(150, Color.LimeGreen)))
                        {
                            graphics.FillEllipse(pulseBrush, pulseRect);
                        }
                        break;

                    case ConnectionStatus.Error:
                        // Draw exclamation mark for error
                        using (var font = new Font("Arial", size / 3, FontStyle.Bold))
                        using (var brush = new SolidBrush(Color.White))
                        {
                            var textRect = new RectangleF(0, 0, size, size);
                            var format = new StringFormat
                            {
                                Alignment = StringAlignment.Center,
                                LineAlignment = StringAlignment.Center
                            };
                            graphics.DrawString("!", font, brush, textRect, format);
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Get the color for a connection status
        /// </summary>
        private static Color GetStatusColor(ConnectionStatus status)
        {
            return status switch
            {
                ConnectionStatus.Disconnected => Color.Red,
                ConnectionStatus.Available => Color.Orange,
                ConnectionStatus.Connecting => Color.Blue,
                ConnectionStatus.Connected => Color.Green,
                ConnectionStatus.Active => Color.LimeGreen,
                ConnectionStatus.Error => Color.DarkRed,
                _ => Color.Gray
            };
        }

        /// <summary>
        /// Create a flashing icon for active status
        /// </summary>
        public static string GetFlashingIconPath(bool isFlashState)
        {
            var status = isFlashState ? ConnectionStatus.Active : ConnectionStatus.Connected;
            return GetStatusIconPath(status);
        }

        /// <summary>
        /// Clean up temporary icon files
        /// </summary>
        public static void CleanupTempIcons()
        {
            try
            {
                if (Directory.Exists(_tempIconPath))
                {
                    Directory.Delete(_tempIconPath, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
