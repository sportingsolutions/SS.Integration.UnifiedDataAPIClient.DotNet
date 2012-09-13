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
using System.ServiceProcess;
using log4net;

namespace SportingSolutions.Udapi.Sdk.StreamingExample.Console
{
    partial class ExampleService : ServiceBase
    {
        private readonly ILog _logger;
        private GTPService _theService;

        public ExampleService()
        {
            InitializeComponent();
            AppDomain.CurrentDomain.UnhandledException += CurrentDomainUnhandledException;
            _logger = LogManager.GetLogger(typeof(ExampleService).ToString());
            _theService = new GTPService();
        }

        void CurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            //by default all exceptions are at least wrapped in exception
            //unless RuntimeCompatibilityAttribute(WrapNonExceptionThrows=true) has been set to false
            _logger.Error("Unhandled Exception, stopping service", (Exception)e.ExceptionObject);
            _theService.Stop();
            _theService = new GTPService();
            _theService.Start();
        }

        protected override void OnStart(string[] args)
        {
            _theService.Start();
        }

        protected override void OnStop()
        {
            _theService.Stop();
        }
    }
}
