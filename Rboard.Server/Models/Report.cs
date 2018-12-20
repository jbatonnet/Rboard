using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace Rboard.Server.Model
{
    public class Report
    {
        public string Path { get; set; }

        public string Name { get; set; }
        public string Category { get; set; }
        public string Author { get; set; }

        public IDictionary<string, string> Configuration { get; } = new Dictionary<string, string>();

        public TimeSpan RefreshTime { get; set; } = TimeSpan.FromHours(1);
        public TimeSpan ArchiveTime { get; set; } = TimeSpan.FromDays(1);
        public TimeSpan DeleteTime { get; set; } = TimeSpan.FromDays(7);
    }
}
