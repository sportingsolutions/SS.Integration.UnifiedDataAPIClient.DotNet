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

using System.Linq;
using System.Text;
using RestSharp;
using log4net;
using System.Net.Http;

namespace SportingSolutions.Udapi.Sdk.Clients
{
    public class RestErrorHelper
    {
        public static void LogResponseError(ILog logger, HttpResponseMessage response, string errorHeading)
        {
            if (logger != null && response != null)
            {
                var stringBuilder = BuildLoggingString(response, errorHeading);
                logger.Error(stringBuilder.ToString());
            }
        }

        public static void LogResponseWarn(ILog logger, HttpResponseMessage response, string warnHeading)
        {
            if (logger != null && response != null)
            {
                var stringBuilder = BuildLoggingString(response, warnHeading);
                logger.Warn(stringBuilder.ToString());
            }
        }

        private static StringBuilder BuildLoggingString(HttpResponseMessage response, string logHeading)
        {
            var stringBuilder = new StringBuilder(logHeading).AppendLine();
            IRestRequest reqq;

            var request = response.RequestMessage;
            if (request != null)
            {
                if (request.RequestUri != null)
                    stringBuilder.AppendFormat("Uri={0}", request.RequestUri).AppendLine();

                stringBuilder.AppendFormat($"Request.Method={request.Method}").AppendLine();
                stringBuilder.AppendFormat($"Request.TimeOut={request..Timeout}").AppendLine();
            }

            stringBuilder.AppendFormat("ResponseStatus={0}", response.ResponseStatus).AppendLine();

            if (response.StatusCode != 0)
            {
                stringBuilder.AppendFormat("StatusCode={0} ({1})", response.StatusCode, response.StatusDescription).AppendLine();
            }

            var transactionId = GetTransactionId(response);
            if (!string.IsNullOrEmpty(transactionId))
            {
                stringBuilder.AppendFormat("TransactionId={0}", GetTransactionId(response)).AppendLine();
            }

            if (!string.IsNullOrEmpty(response.Content))
            {
                stringBuilder.AppendFormat("Content={0}", response.Content).AppendLine();
            }

            if (response.ErrorException != null)
            {
                stringBuilder.AppendFormat("Exception={0}", response.ErrorException).AppendLine();
            }

            return stringBuilder;
        }

        private static string GetTransactionId(HttpResponseMessage response)
        {
            var transactionId = string.Empty;

            var transactionIdHeader = response.Headers.FirstOrDefault(h => h.Key == "TransactionId");
            if (transactionIdHeader != null && transactionIdHeader.Value != null)
            {
                transactionId = transactionIdHeader.Value.ToString();
            }

            return transactionId;
        }
    }
}
