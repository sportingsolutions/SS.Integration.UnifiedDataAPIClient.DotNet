using System;
using System.Net;
using System.Threading;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.StreamingExample.Console.Configuration;
using log4net;
using ICredentials = SportingSolutions.Udapi.Sdk.Interfaces.ICredentials;

namespace SportingSolutions.Udapi.Sdk.StreamingExample.Console.Udapi
{
    internal abstract class BaseSS<T>
    {
        protected ILog _logger;

        //These need to be set by child class to a method that reconnects the session and
        //returns a new Child class with a new session
        protected delegate void ReconnectDelegate();
        protected ReconnectDelegate TheReconnectMethod;

        //The underlying Sporting Solutions session
        private static volatile ISession theSession;
        private static readonly object syncRoot = new Object();
        
        private readonly ISettings _settings;

        protected T _theRealObject;

        //Singleton Session, all child classes spawn from the same session
        protected ISession Session
        {
            get
            {
                if (theSession == null)
                {
                    lock (syncRoot)
                    {
                        if (theSession == null)
                        {
                            _logger.Info("Connecting to UDAPI....");
                            ICredentials credentials = new Credentials { UserName = _settings.User, Password = _settings.Password };
                            theSession = SessionFactory.CreateSession(new Uri(_settings.Url), credentials);
                            _logger.Info("Successfully connected to UDAPI.");
                        }
                    }
                }
                return theSession;
            }
            private set
            {
                if(theSession != null)
                {
                    lock(syncRoot)
                    {
                        if(theSession != null)
                        {
                            theSession = value;
                        }
                    }
                }
            }
        }

        internal BaseSS(ISettings settings = null)
        {
            _settings = settings ?? Settings.Instance;
        }

        protected void ReconnectOnException(Action<T> ctx, T impl)
        {
            ReconnectOnException(x => { ctx(x); return new object(); },impl);
        }

        protected TResult ReconnectOnException<TResult>(Func<T, TResult> ctx, T impl)
        {
            var counter = 0;
            var reconnectSession = false;
            Exception lastException = null;
            var retryDelay = _settings.StartingRetryDelay;//ms
            while (counter < _settings.MaxRetryAttempts)
            {
                try
                {
                    return ctx(impl);
                }
                catch (WebException wex)
                {
                    lastException = wex;
                    counter++;
                    if (counter == _settings.MaxRetryAttempts)
                    {
                        _logger.Error(
                              String.Format("Failed to successfully execute Sporting Solutions method after all {0} attempts",
                                            _settings.MaxRetryAttempts), wex);
                    }
                    else
                    {
                        _logger.WarnFormat("Failed to successfully execute Sporting Solutions method on attempt {0}. Stack Trace:{1}", counter, wex.StackTrace);                        
                    }

                    if (wex.Response != null)
                    {
                        var webResponse = (HttpWebResponse) wex.Response;
                        if (webResponse.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            reconnectSession = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    counter++;
                    if (counter == _settings.MaxRetryAttempts)
                    {
                        _logger.Error(
                              String.Format("Failed to successfully execute Sporting Solutions method after all {0} attempts",
                                            _settings.MaxRetryAttempts), ex);
                    }
                    else
                    {
                        _logger.WarnFormat("Failed to successfully execute Sporting Solutions method on attempt {0}. Stack Trace:{1}", counter, ex.StackTrace);    
                    }
                }

                retryDelay = 2 * retryDelay;
                if(retryDelay > _settings.MaxRetryDelay)
                {
                    retryDelay = _settings.MaxRetryDelay;
                }
                _logger.DebugFormat("Retrying Sporting Solutions API in {0} ms", retryDelay);
                Thread.Sleep(retryDelay);
                Init(reconnectSession);
            }
            throw lastException ?? new Exception();
        }

        protected void Init(bool connectSession)
        {
            var counter = 0;
            Exception lastException = null;
            var retryDelay = _settings.StartingRetryDelay;//ms
            while (counter < _settings.MaxRetryAttempts)
            {
                try
                {
                    if (connectSession)
                    {
                        Session = null;
                    }
                    TheReconnectMethod();
                    return;
                }
                catch (WebException wex)
                {
                    lastException = wex;
                    counter++;
                    if (counter == _settings.MaxRetryAttempts)
                    {
                        _logger.Error(
                              String.Format("Failed to successfully execute Sporting Solutions method after all {0} attempts",
                                            _settings.MaxRetryAttempts), wex);
                    }
                    else
                    {
                        _logger.WarnFormat("Failed to successfully execute Sporting Solutions method on attempt {0}. Stack Trace:{1}", counter, wex.StackTrace);
                    }

                    if (wex.Response != null)
                    {
                        var webResponse = (HttpWebResponse)wex.Response;
                        if (webResponse.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            connectSession = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    counter++;
                    if (counter == _settings.MaxRetryAttempts)
                    {
                        _logger.Error(
                              String.Format("Failed to successfully execute Sporting Solutions method after all {0} attempts",
                                            _settings.MaxRetryAttempts), ex);
                    }
                    else
                    {
                        _logger.WarnFormat("Failed to successfully execute Sporting Solutions method on attempt {0}. Stack Trace:{1}", counter, ex.StackTrace);
                    }
                }

                retryDelay = 2 * retryDelay;
                if (retryDelay > _settings.MaxRetryDelay)
                {
                    retryDelay = _settings.MaxRetryDelay;
                }
                _logger.DebugFormat("Retrying Sporting Solutions API in {0} ms", retryDelay);
                Thread.Sleep(retryDelay);
            }
            throw lastException ?? new Exception();
        }
    }
}
