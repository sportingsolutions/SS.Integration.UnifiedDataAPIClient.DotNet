//Copyright 2012 Spin Services Limited

//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at

//    http://www.apache.org/licenses/LICENSE-2.0

//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
//using System.ServiceProcess;
using System.Threading.Tasks;
using log4net;

namespace SportingSolutions.Udapi.Sdk.StreamingExample.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            var path = Assembly.GetExecutingAssembly().Location;
            var fileInfo = new FileInfo(path);
            var dir = fileInfo.DirectoryName;
            //log4net.Config.XmlConfigurator.ConfigureAndWatch(new FileInfo(string.Format("{0}\\log4net.config", dir)));
            log4net.Config.XmlConfigurator.ConfigureAndWatch(LogManager.GetRepository(Assembly.GetEntryAssembly()) , new FileInfo(string.Format("{0}\\log4net.config", dir)));
            
            var logger = LogManager.GetLogger(typeof(Program));

            try
            {                
                //var servicesToRun = new ServiceBase[] 
                //{ 
                //    new ExampleService() 
                //};

                if (Environment.UserInteractive)
                {
                    //var type = typeof(ServiceBase);
                    //const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    //var method = type.GetMethod("OnStart", flags);

                    //foreach (var service in servicesToRun)
                    //{
                    //    method.Invoke(service, new object[] { null });
                    //}

                    var service = new ExampleService();
                    service.OnStart(args);

                    logger.Debug(@"Service Started! - Press any key to stop");
                    System.Console.ReadLine();
                }
                else
                {
                    //ServiceBase.Run(servicesToRun);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                throw;
            }
            
        }
    }
}
