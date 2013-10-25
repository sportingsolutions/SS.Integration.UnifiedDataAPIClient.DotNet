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

namespace SportingSolutions.Udapi.Sdk.Clients
{
    public class RestErrorHelper
    {
        public static void LogRestError(ILog logger, IRestResponse restResponse, string errorHeading)
        {
            if (logger != null && restResponse != null)
            {
                var stringBuilder = new StringBuilder(errorHeading).AppendLine();

                if (restResponse.Request != null)
                {
                    stringBuilder.AppendFormat("Request.Method={0}", restResponse.Request.Method).AppendLine();
                    stringBuilder.AppendFormat("Request.TimeOut={0}", restResponse.Request.Timeout).AppendLine();
                }

                stringBuilder.AppendFormat("Uri={0}", restResponse.ResponseUri != null ? restResponse.ResponseUri.ToString() : restResponse.Request.Resource).AppendLine();
                stringBuilder.AppendFormat("ResponseStatus={0}", restResponse.ResponseStatus).AppendLine();

                if (restResponse.StatusCode != 0)
                {
                    stringBuilder.AppendFormat("StatusCode={0} ({1})", restResponse.StatusCode, restResponse.StatusDescription).AppendLine();
                }

                var transactionId = GetTransactionId(restResponse);
                if (!string.IsNullOrEmpty(transactionId))
                {
                    stringBuilder.AppendFormat("TransactionId={0}", GetTransactionId(restResponse)).AppendLine();
                }

                if (!string.IsNullOrEmpty(restResponse.Content))
                {
                    stringBuilder.AppendFormat("Content={0}", restResponse.Content).AppendLine();
                }

                if (restResponse.ErrorException != null)
                {
                    stringBuilder.AppendFormat("Exception={0}", restResponse.ErrorException).AppendLine();
                }
                
                logger.Error(stringBuilder.ToString());
            }
        }

        private static string GetTransactionId(IRestResponse restResponse)
        {
            var transactionId = string.Empty;

            var transactionIdHeader = restResponse.Headers.FirstOrDefault(h => h.Name == "TransactionId");
            if (transactionIdHeader != null && transactionIdHeader.Value != null)
            {
                transactionId = transactionIdHeader.Value.ToString();
            }

            return transactionId;
        }
    }
}
