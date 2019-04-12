using System;

namespace Rboard.Server.Model
{
    public abstract class Report
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public string Author { get; set; }

        public TimeSpan RefreshTime { get; set; } = TimeSpan.FromHours(1);
    }
}
