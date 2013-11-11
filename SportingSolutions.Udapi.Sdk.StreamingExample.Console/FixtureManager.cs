using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace SportingSolutions.Udapi.Sdk.StreamingExample.Console
{
    public class FixtureManager
    {
        private FileSystemWatcher _watcher;
        private string _filePath;
        public FixtureManager(string fileName)
        {
            _filePath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), fileName);
            CreateFile(_filePath);
            _watcher = new FileSystemWatcher(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), fileName);
            _watcher.Changed += ConfigurationUpdated;
            _watcher.EnableRaisingEvents = true;

        }

        private void CreateFile(string fileName)
        {
            if (!File.Exists(fileName))
                File.Create(fileName);
        }

        private void ConfigurationUpdated(object sender, FileSystemEventArgs fileSystemEventArgs)
        {

            if (fileSystemEventArgs.ChangeType != WatcherChangeTypes.Changed) return;

            string command = null;

            var getCommand = new Func<string>(() =>
            {
                lock (_watcher)
                {
                    using (var sr = new StreamReader(_filePath))
                    {
                        command = sr.ReadLine();
                    }
                    using (var sw = new StreamWriter(_filePath, false))
                    {
                        sw.Write(String.Empty);
                    }

                    return command != null ? command.Trim() : command;
                }
            });

            int maxTries = 0;
            while (maxTries < 3)
            {
                try
                {
                    getCommand();
                    maxTries = 3;
                }
                catch (Exception)
                {
                    maxTries++;
                    Thread.Sleep(1000);
                }
            }


            if (string.IsNullOrEmpty(command)) return;

            if (command.StartsWith("STOP:"))
            {
                var fixtureId = command.Substring(5);
                FixtureController.StopFixture(fixtureId);
            }

            if (command.StartsWith("BLOCK:"))
            {
                var fixtureId = command.Substring(6);
                FixtureController.StopFixture(fixtureId, true);
            }

            if (command.StartsWith("RESTART:"))
            {
                var fixtureId = command.Substring("RESTART:".Length);
                FixtureController.RestartFixture(fixtureId);
            }
        }



    }
}
