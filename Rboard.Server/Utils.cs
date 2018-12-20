using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;

namespace Rboard.Server
{
    public static class Utils
    {
        public static TimeSpan ParseTime(string time)
        {
            time = time.Trim().TrimEnd('s');

            string[] parts = time.Split(' ');
            if (parts.Length != 2)
                throw new FormatException("Could not parse " + time + " as a valid time span");

            int count;
            if (!int.TryParse(parts[0], out count))
                throw new FormatException("Could not parse " + time + " as a valid time span");

            string unit = parts[1].ToLower();
            switch (unit)
            {
                case "second": return TimeSpan.FromSeconds(count);
                case "minute": return TimeSpan.FromMinutes(count);
                case "hour": return TimeSpan.FromHours(count);
                case "day": return TimeSpan.FromDays(count);
                case "week": return TimeSpan.FromDays(7 * count);
                case "month": return TimeSpan.FromDays(30 * count);
                case "year": return TimeSpan.FromDays(365 * count);
            }

            throw new FormatException("Could not parse " + time + " as a valid time span");
        }
        public static DateTime RoundDateTime(DateTime dateTime, TimeSpan interval)
        {
            return new DateTime(dateTime.Ticks - dateTime.Ticks % interval.Ticks);
        }
        public static string FormatDateTime(DateTime dateTime, string shortFormat, string longFormat)
        {
            return dateTime.ToString(dateTime.Date == dateTime ? shortFormat : longFormat);
        }
    }
}
