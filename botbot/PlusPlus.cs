using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using MongoDB.Driver;

namespace botbot
{
    public class PlusPlus
    {
        public static bool Check(string message)
        {
            string pattern = "^([\\s\\w'@.\\-:\u3040-\u30FF\uFF01-\uFF60\u4E00-\u9FA0]*)\\s*(\\+\\+|--|—)(?:\\s+(?:for|because|cause|cuz|as)\\s+(.+))?$";
            Regex regex = new Regex(pattern);
            Match match = regex.Match(message);
            if (match.Success)
            {
                string name = match.Groups[1].Value;
                string vote = match.Groups[2].Value;
                string reason = match.Groups[3].Value;
                reason = reason.Trim().ToLower();
                if (!string.IsNullOrEmpty(name))
                {
                    if (name[0] == ':')
                    {
                        name = Regex.Replace(name, "(^\\s*@)|([,\\s]*$)", "").Trim().ToLower();
                    }
                    else
                    {
                        name = Regex.Replace(name, "(^\\s*@)|([,:\\s]*$)", "").Trim().ToLower();
                    }
                }
            }
            return match.Success;
        }
    }
}