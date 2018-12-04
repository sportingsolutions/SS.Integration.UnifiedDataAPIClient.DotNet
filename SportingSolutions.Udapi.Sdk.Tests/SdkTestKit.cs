using Akka.TestKit.NUnit;

namespace SportingSolutions.Udapi.Sdk.Tests
{
    public class SdkTestKit : TestKit
    {
        #region Constants

        public const int ASSERT_WAIT_TIMEOUT = 9500 /*ms*/;
        public const int ASSERT_EXEC_INTERVAL = 500 /*ms*/;

        #endregion
    }
}
