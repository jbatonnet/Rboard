using System;
using System.Collections.Generic;

namespace Rboard.Server.Model
{
    public class RReport : Report
    {
        public string Path { get; set; }

        public IDictionary<string, string> Configuration { get; } = new Dictionary<string, string>();
        public IList<string> Libraries { get; } = new List<string>();

        public TimeSpan ArchiveTime { get; set; } = TimeSpan.FromDays(1);
        public TimeSpan DeleteTime { get; set; } = TimeSpan.FromDays(7);
    }
}
