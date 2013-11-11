using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SportingSolutions.Udapi.Sdk.StreamingExample.Console.Model
{
    public class TaskWrapper
    {
        public string Identifier { get; private set; }
        public CancellationTokenSource CancellationToken { get; private set; }
        public Task Worker { get; private set; }
        public Action Action { get; private set; }

        public TaskWrapper(Task worker, Action action, CancellationTokenSource cancellationToken, string taskIdentifier)
        {
            CancellationToken = cancellationToken;
            Worker = worker;
            Action = action;
            Identifier = taskIdentifier;
        }

        public void CancelTask()
        {
            CancellationToken.Cancel();
        }
    }
}
