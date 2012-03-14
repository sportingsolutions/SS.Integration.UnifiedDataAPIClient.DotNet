using System;
using System.Collections.Specialized;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Cache;
using System.Text;

namespace SportingSolutions.Udapi.Sdk.Clients
{
    public static class RestHelper
    {
        private static HttpWebRequest CreateRequest(Uri url, string data, string httpMethod, string contentType, NameValueCollection headers, int timeout, bool gzip)
        {
            var request = WebRequest.Create(url);

            request.Method = httpMethod.ToUpper();
            request.ContentType = contentType;
            request.Timeout = timeout;
            request.CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.NoCacheNoStore);

            if (headers != null)
                request.Headers.Add(headers);

            if (gzip)
                request.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip");

            if (data != null)
            {
                var byteData = new UTF8Encoding().GetBytes(data);
                request.ContentLength = byteData.Length;

                using (var postStream = request.GetRequestStream())
                {
                    postStream.Write(byteData, 0, byteData.Length);
                }
            }
            else if (request.Method.ToUpper() != "GET")
            {
                //Specify a zero content length to prevent 411s
                request.ContentLength = 0;
            }

            return request as HttpWebRequest;
        }

        public static string GetResponse(Uri url, string data, string httpMethod, string contentType, NameValueCollection headers, int timeout = 30000, bool gzip = true)
        {
            var request = CreateRequest(url, data, httpMethod, contentType, headers, timeout, gzip);

            using (var response = request.GetResponse() as HttpWebResponse)
            {
                if (response == null)
                    throw new NullReferenceException("Response was null");

                //Get the response stream
                var responseStream = response.GetResponseStream();

                if (response.ContentEncoding.ToLower().Contains("gzip"))
                    responseStream = new GZipStream(responseStream, CompressionMode.Decompress);

                //Create response stream reader
                if (responseStream == null)
                    throw new NullReferenceException("Response Stream was null");

                var responseStreamReader = new StreamReader(responseStream, Encoding.Default);
                return responseStreamReader.ReadToEnd();
            }
        }

        public static string GetResponse(HttpWebResponse response)
        {
            var responseStream = response.GetResponseStream();

            try
            {
                if (responseStream == null)
                    throw new NullReferenceException("Response Stream was null");

                if (response.ContentEncoding.ToLower().Contains("gzip"))
                    responseStream = new GZipStream(responseStream, CompressionMode.Decompress);

                var responseStreamReader = new StreamReader(responseStream, Encoding.Default);
                return responseStreamReader.ReadToEnd();
            }
            finally
            {
                if (responseStream != null)
                    responseStream.Dispose();
            }

        }

        public static HttpWebResponse GetResponseEx(Uri url, string data, string httpMethod, string contentType, NameValueCollection headers, int timeout = 30000, bool gzip = true)
        {
            var request = CreateRequest(url, data, httpMethod, contentType, headers, timeout, gzip);
            return request.GetResponse() as HttpWebResponse;
        }
    }
}
