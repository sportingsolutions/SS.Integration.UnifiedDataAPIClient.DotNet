using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace SportingSolutions.Udapi.Sdk
{
    public class TestLogger// : IDisposable
    {
        private static string DEFAULT_DIRNAME = @"C:\\log\\Udapi";
        private static string DEFAULT_FILENAME = "TestLogger.log";

        private readonly string _fileName;
        private static readonly TestLogger _instance = new TestLogger();

        private object _lockObject = new object();

        public static TestLogger Instance => _instance;

        private TestLogger()
        {
            var fileNameConfig = ConfigurationManager.AppSettings["TestLogger"];
            var defaultFileName = Path.Combine(DEFAULT_DIRNAME, DEFAULT_FILENAME);
            _fileName = fileNameConfig == null ? defaultFileName : fileNameConfig;

            if (!Directory.Exists(DEFAULT_DIRNAME))
                Directory.CreateDirectory(DEFAULT_DIRNAME);

            if (!File.Exists(_fileName))
                File.Create(_fileName);
        }

        public void WriteLine(string message, bool withCallStack = false)
        {
            lock (_lockObject)
            {
                string text = $"MyLogger {DateTime.Now}.{DateTime.Now.Millisecond}: {message}{Environment.NewLine}";
                if (withCallStack)
                    text += Environment.StackTrace + Environment.NewLine;
                File.AppendAllText(_fileName, text);

            }
        }

    }
}
