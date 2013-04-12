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
using System.Threading;

namespace SportingSolutions.Udapi.Sdk.Clients
{
    public static class RestHelper
    {
        public static Response GetResponse(Uri url, string data, string httpMethod, string contentType,
                                           NameValueCollection headers, int timeout = 30000, bool gzip = true)
        {
            // Create Request

            var request = CreateRequest(url, data, httpMethod, contentType, headers, gzip);

            // Invoke Async

            var waitHandle = new ManualResetEvent(false);
            var asyncRequest = new AsyncRequestState(request, waitHandle);
            request.BeginGetResponse(AsyncResponse, asyncRequest);

            // Wait for reponse or timeout

            var success = waitHandle.WaitOne(timeout);

            if (!success)
            {
                throw new TimeoutException("Web request did not complete in a timely fashion");
            }

            if (asyncRequest.UnexpectedException != null)
            {
                throw asyncRequest.UnexpectedException;
            }

            // Return response

            return new Response
                {
                    StatusCode = asyncRequest.ResponseStatusCode,
                    Content = asyncRequest.ResponseContent,
                    Headers = asyncRequest.ResponseHeaders
                };
        }

        private static HttpWebRequest CreateRequest(Uri url, string data, string httpMethod, string contentType,
                                                    NameValueCollection headers, bool gzip)
        {
            var request = WebRequest.Create(url);

            request.Method = httpMethod.ToUpper();
            request.ContentType = contentType;
            request.CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.NoCacheNoStore);

            if (headers != null)
            {
                request.Headers.Add(headers);
            }

            if (gzip)
            {
                request.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip");
            }

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

        private static void AsyncResponse(IAsyncResult result)
        {
            var asyncState = (AsyncRequestState) result.AsyncState;

            try
            {
                using (var response = asyncState.Request.EndGetResponse(result) as HttpWebResponse)
                {
                    ProcessWebResponse(asyncState, response);
                }
            }
            catch (Exception ex)
            {
                var webException = ex as WebException;

                if (webException != null)
                {
                    using (var response = webException.Response as HttpWebResponse)
                    {
                        ProcessWebResponse(asyncState, response);
                    }
                }
                else
                {
                    asyncState.UnexpectedException = ex;
                }
            }
            finally
            {
                asyncState.WaitHandle.Set();
            }
        }

        private static void ProcessWebResponse(AsyncRequestState asyncState, HttpWebResponse response)
        {
            if (response != null)
            {
                asyncState.ResponseStatusCode = response.StatusCode;
                asyncState.ResponseHeaders = response.Headers;

                var responseStream = response.GetResponseStream();

                if (responseStream != null)
                {
                    if (response.ContentEncoding.ToLower().Contains("gzip"))
                    {
                        responseStream = new GZipStream(responseStream, CompressionMode.Decompress);
                    }

                    using (responseStream)
                    using (var responseStreamReader = new StreamReader(responseStream, Encoding.Default))
                    {
                        asyncState.ResponseContent = responseStreamReader.ReadToEnd();
                    }
                }
            }
        }
    }

    public class Response
    {
        public string Content { get; set; }
        public HttpStatusCode StatusCode { get; set; }
        public WebHeaderCollection Headers { get; set; }
    }

    internal class AsyncRequestState
    {
        internal AsyncRequestState(HttpWebRequest request, ManualResetEvent waitHandle)
        {
            Request = request;
            WaitHandle = waitHandle;
        }

        internal HttpWebRequest Request { get; private set; }
        internal ManualResetEvent WaitHandle { get; private set; }
        internal Exception UnexpectedException { get; set; }
        internal string ResponseContent { get; set; }
        internal HttpStatusCode ResponseStatusCode { get; set; }
        internal WebHeaderCollection ResponseHeaders { get; set; }
    }
}
