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
