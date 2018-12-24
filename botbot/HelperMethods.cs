using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace botbot
{
    public static class HelperMethods
    {
        public static string ParseSlackUrl(string url)
        {
            // Slack urls look like <http://www.foo.com|www.foo.com> when parsed by Slack
            // They only look like <http://www.foo.com> when Slack didn't do extra parsing
            string[] splitUrl = url.Split('|');
            // Get the url minus the first '<'
            string parsedUrl = splitUrl[0].Substring(1);
            if (splitUrl.Length == 1)
            {
                // Then if we got a non-parsed url take the last '>' off
                parsedUrl = parsedUrl.Substring(0, parsedUrl.Length - 1);
            }
            return parsedUrl;
        }
    }
}
