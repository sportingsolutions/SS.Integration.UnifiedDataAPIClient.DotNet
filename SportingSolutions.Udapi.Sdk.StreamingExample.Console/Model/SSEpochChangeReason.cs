using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SportingSolutions.Udapi.Sdk.StreamingExample.Console.Model
{
    public enum SSEpochChangeReason
    {
        Created = 0,
        Unpublished = 5,
        Deleted = 10,
        Participants = 20,
        StartTime = 30,
        MatchStatus = 40,
        BaseVariables = 50,
        Definition = 60
    }
}
