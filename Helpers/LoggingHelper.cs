using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

namespace IgusSwarovskiHmi.Helpers
{
    public static class LoggingHelper
    {

        static LoggingHelper()
        {
            InitializeLogger();
            LogTextBox.TextChanged += (s, e) =>
            {
                LogTextBox.ScrollToEnd();
            };

            LogTextBox.Cursor = System.Windows.Input.Cursors.Arrow;
        }

        public static RichTextBox LogTextBox { get; } = new RichTextBox()
        {
            IsReadOnly = true,
            Foreground = Brushes.Black,
            Background = Brushes.DimGray,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Cascadia Code"),
            FontSize = 15
        };

        public static void InitializeLogger()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose().
                WriteTo.RichTextBox(LogTextBox).CreateLogger();

            Log.Information("Logger initialized");

        }

    }
}
