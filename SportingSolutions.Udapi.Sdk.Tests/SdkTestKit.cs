using Akka.TestKit.NUnit;

namespace SportingSolutions.Udapi.Sdk.Tests
{
    public class SdkTestKit : TestKit
    {
        #region Constants

        public const int ASSERT_WAIT_TIMEOUT = 10000 /*ms*/;
        public const int ASSERT_EXEC_INTERVAL = 200 /*ms*/;

        #endregion
    }
}
