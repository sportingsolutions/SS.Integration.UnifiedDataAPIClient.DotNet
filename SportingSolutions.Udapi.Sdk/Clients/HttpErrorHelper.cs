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
using SportingSolutions.Udapi.Sdk.Extensions;
using System;

namespace SportingSolutions.Udapi.Sdk.Clients
{
    public class HttpErrorHelper
    {
        public static void LogResponseError(ILog logger, HttpResponseMessage response, string errorHeading, Exception ex = null)
        {
            if (logger != null && response != null)
            {
                var stringBuilder = BuildLoggingString(response, errorHeading, ex);
                logger.Error(stringBuilder.ToString());
            }
        }

        public static void LogResponseWarn(ILog logger, HttpResponseMessage response, string warnHeading, Exception ex = null)
        {
            if (logger != null && response != null)
            {
                var stringBuilder = BuildLoggingString(response, warnHeading, ex);
                logger.Warn(stringBuilder.ToString());
            }
        }

        private static StringBuilder BuildLoggingString(HttpResponseMessage response, string logHeading, Exception ex = null)
        {
            var stringBuilder = new StringBuilder(logHeading).AppendLine();

            var request = response.RequestMessage;
            if (request != null)
            {
                if (request.RequestUri != null)
                    stringBuilder.AppendFormat($"Uri={request.RequestUri}").AppendLine();

                stringBuilder.AppendFormat($"Request.Method={request.Method}").AppendLine();
                stringBuilder.AppendFormat($"Request.TimeOut={request.GetTimeout()?.Milliseconds}").AppendLine();
            }

            stringBuilder.AppendFormat($"IsSuccessStatusCode={response.IsSuccessStatusCode}").AppendLine();

            if (response.StatusCode != 0)
                stringBuilder.AppendFormat($"StatusCode={response.StatusCode} ({response.StatusCode.ToString()})").AppendLine();

            var transactionId = GetTransactionId(response);
            if (!string.IsNullOrEmpty(transactionId))
            {
                stringBuilder.AppendFormat("TransactionId={0}", GetTransactionId(response)).AppendLine();
            }

            var contestStr = response.Content.ReadAsStringAsync().Result;
            if (!string.IsNullOrWhiteSpace(contestStr))
                stringBuilder.AppendFormat($"Content={contestStr}").AppendLine();

            if (ex != null)
                stringBuilder.AppendFormat($"Exception={ex}").AppendLine();

            return stringBuilder;
        }

        private static string GetTransactionId(HttpResponseMessage response)
        {
            var transactionId = string.Empty;

            var transactionIdHeader = response.Headers.FirstOrDefault(h => h.Key == "TransactionId");
            if (transactionIdHeader.Value != null)
                transactionId = transactionIdHeader.Value.FirstOrDefault() ?? string.Empty;

            return transactionId;
        }
    }
}
