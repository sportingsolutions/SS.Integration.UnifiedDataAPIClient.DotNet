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

namespace SportingSolutions.Udapi.Sdk.Interfaces
{
    internal interface IEchoController : IDisposable
    {
        /// <summary>
        ///     Adds a new IConsumer to the IEchoController.
        /// </summary>
        /// <param name="consumer"></param>
        void AddConsumer(IConsumer consumer);

        /// <summary>
        /// 
        ///     Removes an IConsumer from the IEchoController.
        /// 
        ///     Note that this method does not raise 
        ///     any disconnection event when called.
        /// 
        /// </summary>
        /// <param name="consumer"></param>
        void RemoveConsumer(IConsumer consumer);

        /// <summary>
        ///     Removes all the registred IConsumers
        /// </summary>
        void RemoveAll();

        /// <summary>
        /// 
        ///     Allows to inform the IEchoController
        ///     that an echo message has arrived
        ///     for the IConsumer whose Id is consumerId
        /// 
        /// </summary>
        /// <param name="consumerId"></param>
        void ProcessEcho(string consumerId);
    }
}
