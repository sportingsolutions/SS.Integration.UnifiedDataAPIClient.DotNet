﻿//Copyright 2012 Spin Services Limited

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

namespace SportingSolutions.Udapi.Sdk.StreamingExample.Console
{
    partial class ExampleService : ServiceBase
    {
        private readonly GTPService _theService;

        public ExampleService()
        {
            InitializeComponent();
            AppDomain.CurrentDomain.UnhandledException += CurrentDomainUnhandledException;
            _theService = new GTPService();
        }
        void CurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            _theService.Stop();
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
