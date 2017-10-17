using System.Collections.Generic;
using Akka.TestKit.NUnit;
using NUnit.Framework;
using SportingSolutions.Udapi.Sdk.Clients;

namespace SportingSolutions.Udapi.Sdk.Tests
{
    public class SdkTestKit : TestKit
    {
        #region Constants

        public const int ASSERT_WAIT_TIMEOUT = 10000 /*ms*/;
        public const int ASSERT_EXEC_INTERVAL = 200 /*ms*/;

        #endregion

        #region Fields

        private static readonly List<string> UseSingleQueueStreamingMethodTestList = new List<string>(50);

        #endregion

        #region Properties

        protected string CurrentTestFullName => TestContext.CurrentContext.Test.FullName;

        protected bool UseSingleQueueStreamingMethod
        {
            get => ((Configuration) UDAPI.Configuration).UseSingleQueueStreamingMethod;
            set => ((Configuration) UDAPI.Configuration).UseSingleQueueStreamingMethod = value;
        }

        #endregion

        #region Protected methods

        protected void SetupUseSingleQueueStreamingMethodSetting()
        {
            if (UseSingleQueueStreamingMethodTestList.Contains(CurrentTestFullName))
            {
                UseSingleQueueStreamingMethod = true;
            }
            else
            {
                UseSingleQueueStreamingMethod = false;
                UseSingleQueueStreamingMethodTestList.Add(CurrentTestFullName);
            }
        }

        #endregion
    }
}
