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
using System.Collections.Specialized;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Cache;
using System.Text;
using SportingSolutions.Udapi.Sdk.Exceptions;

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

        public static string GetResponseWithWebException(Uri url, string data, string httpMethod, string contentType, NameValueCollection headers, int timeout = 30000, bool gzip = true)
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

        public static string GetResponse(Uri url, string data, string httpMethod, string contentType, NameValueCollection headers, int timeout = 30000, bool gzip = true)
        {
            try
            {
                return GetResponseWithWebException(url, data, httpMethod, contentType, headers, timeout, gzip);
            }
            catch (WebException ex)
            {
                using(var response = ex.Response)
                {
                    var httpResponse = (HttpWebResponse) response;
                    if (httpResponse != null)
                    {
                        using (var rdata = httpResponse.GetResponseStream())
                        {
                            if (rdata != null)
                            {
                                var text = new StreamReader(rdata).ReadToEnd();
                                if(httpResponse.StatusCode == HttpStatusCode.Unauthorized)
                                {
                                    throw new NotAuthenticatedException(text,ex);
                                }
                                else
                                {
                                    throw new Exception(text, ex);
                                }
                            }
                            throw new Exception(ex.Message,ex);
                        }
                    }
                    else
                    {
                        throw new Exception(ex.Message, ex);
                    }
                }
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
