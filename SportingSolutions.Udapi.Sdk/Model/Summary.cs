using System.Collections.Generic;

namespace SportingSolutions.Udapi.Sdk.Model
{
    public class Summary
    {
        public string Id { get; set; }

        public string Description { get; set; }

        public List<Participant> Participants { get; set; }

        public string Date { get; set; }

        public List<Tag> Tags { get; set; }

        public string DefinitionId { get; set; }

        public string DefinitionName { get; set; }

        public string Type { get; set; }
    }
}
