using System;

namespace OctoDB
{
    public class HistoryEntry
    {
        public string Id { get; set; }
        public DateTimeOffset Modified { get; set; }
        public string Hash { get; set; }
    }
}