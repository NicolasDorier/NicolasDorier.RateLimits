using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NicolasDorier.RateLimits
{
    /// <summary>
    /// Represent a description of how to throttle requests
    /// It uses the same semantic as https://www.nginx.com/blog/rate-limiting-nginx/
    /// </summary>
    public class LimitRequestZone
    {
        public LimitRequestZone(string name, int? burst, bool nodelay, RequestRate requestRate)
        {
            if (requestRate == null)
                throw new ArgumentNullException(nameof(requestRate));
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));
            Name = name;
            Burst = burst;
            NoDelay = nodelay;
            RequestRate = requestRate;
        }

        public static bool TryParse(string str, out LimitRequestZone limitRequestZone)
        {
            if (str == null)
                throw new ArgumentNullException(nameof(str));
            limitRequestZone = null;
            str = str.Trim();
            var parts = str.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim().ToLowerInvariant()).ToList();


            string name = null;
            int? burst = null;
            bool nodelay = false;
            RequestRate requestRate = null;
            foreach(var p in parts)
            {
                if(p.StartsWith("zone=", StringComparison.OrdinalIgnoreCase))
                {
                    if (name != null)
                        return false;
                    name = p.Substring("zone=".Length);
                    var split = name.IndexOf(":", StringComparison.OrdinalIgnoreCase);
                    if (split != -1)
                        return false;
                }
                else if (p.StartsWith("burst=", StringComparison.OrdinalIgnoreCase))
                {
                    if (burst != null)
                        return false;
                    if (!int.TryParse(p.Substring("burst=".Length), out int v))
                        return false;
                    if (v <= 0)
                        return false;
                    burst = v;
                }
                else if(p.Equals("nodelay", StringComparison.OrdinalIgnoreCase))
                {
                    if (nodelay)
                        return false;
                    nodelay = true;
                }
                else if (p.StartsWith("rate=", StringComparison.OrdinalIgnoreCase))
                {
                    if (requestRate != null)
                        return false;
                    var v = p.Substring("rate=".Length);
                    if (!RequestRate.TryParse(v, out requestRate))
                        return false;
                }
                else
                {
                    return false;
                }
            }
            if (requestRate == null || string.IsNullOrEmpty(name))
                return false;
            limitRequestZone = new LimitRequestZone(name, burst, nodelay, requestRate); ;
            return true;
        }
        public string Name { get; }
        public int? Burst { get; }
        public bool NoDelay { get; }
        public RequestRate RequestRate { get; }

        public override string ToString()
        {
            List<string> values = new List<string>();
            values.Add($"zone={Name}");
            values.Add($"rate={RequestRate}");
            if(Burst.HasValue)
            {
                values.Add($"burst={Burst}");
            }
            if(NoDelay)
            {
                values.Add($"nodelay");
            }
            return String.Join(" ", values.ToArray());
        }
    }
}
