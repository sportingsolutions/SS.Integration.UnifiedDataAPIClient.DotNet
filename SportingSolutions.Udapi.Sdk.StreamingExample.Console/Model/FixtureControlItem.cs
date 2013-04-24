using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SportingSolutions.Udapi.Sdk.StreamingExample.Console.Model
{
    public class FixtureControlItem<T>
    {
        public string Id { get; private set; }
        private readonly Func<T> _createItem;
        public T Item { get; private set; }
        public FixtureStatus FixtureStatus { get; set; }

        public FixtureControlItem(string id, Func<T> createItem, FixtureStatus status)
        {
            _createItem = createItem;
            Item = createItem();
            Id = id;
            FixtureStatus = status;
        }

        public FixtureControlItem<T> ReCreate()
        {
            return new FixtureControlItem<T>(Id, _createItem, FixtureStatus.Active);
        }
    }

    public enum FixtureStatus
    {
        Stopped,
        Active,
        Blocked,
        Completed
    }
}
