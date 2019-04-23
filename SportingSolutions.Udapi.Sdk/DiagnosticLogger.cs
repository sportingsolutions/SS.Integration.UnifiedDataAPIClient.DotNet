using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using log4net;

namespace SportingSolutions.Udapi.Sdk
{
    internal static class DiagnosticLogger
    {
        private static ILog _logger = LogManager.GetLogger(typeof(DiagnosticLogger));
        private static Process _currentProcess = Process.GetCurrentProcess();


        private static void LogMain()
        {
            var info = CollectInfo();
            _logger.Debug(info);
        }

        public static void LogElapsed(string process, TimeSpan elapsed)
        {
            LogMain();
            _logger.Debug($"{process} elapsed: {elapsed.Milliseconds} ms");
        }

        public static string CollectInfo()
        {
            StringBuilder result = new StringBuilder();
            result.Append($"Diagnostics info:{Environment.NewLine}");
            result.Append($"physicalMemory={_currentProcess.WorkingSet64}{Environment.NewLine}");
            result.Append($"; maxPhysicalMemory={_currentProcess.PeakWorkingSet64}{Environment.NewLine}");
            result.Append($"; virtualMemory={_currentProcess.VirtualMemorySize64}{Environment.NewLine}");
            result.Append($"; maxVirtualMemory={_currentProcess.PeakVirtualMemorySize64}{Environment.NewLine}");
            result.Append($"; threads={_currentProcess.Threads}{Environment.NewLine}");
            return result.ToString();
        }

    }
}
