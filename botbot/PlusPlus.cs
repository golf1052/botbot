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
        public static string Check(string message, string channel, string user)
        {
            string pattern = "^([\\s\\w'@.\\-:\u3040-\u30FF\uFF01-\uFF60\u4E00-\u9FA0]*)\\s*(\\+\\+|--|—)(?:\\s+(?:for|because|cause|cuz|as)\\s+(.+))?$";
            Regex regex = new Regex(pattern);
            Match match = regex.Match(message);
            if (match.Success)
            {
                string thing = match.Groups[1].Value;
                string vote = match.Groups[2].Value;
                string reason = match.Groups[3].Value;
                reason = reason.Trim().ToLower();
                if (!string.IsNullOrEmpty(thing))
                {
                    if (thing[0] == ':')
                    {
                        thing = Regex.Replace(thing, "(^\\s*@)|([,\\s]*$)", "").Trim().ToLower();
                    }
                    else
                    {
                        thing = Regex.Replace(thing, "(^\\s*@)|([,:\\s]*$)", "").Trim().ToLower();
                    }
                }

                if (string.IsNullOrEmpty(thing))
                {
                    var last = PlusPlusScore.Last(channel);
                    if (last != null)
                    {
                        thing = last.Thing;
                        if (!string.IsNullOrEmpty(last.Reason))
                        {
                            reason = last.Reason;
                        }
                    }
                }

                KeyValuePair<int?, int?> scores;
                if (vote == "++")
                {
                    scores = PlusPlusScore.Add(thing, user, channel, reason);
                }
                else
                {
                    scores = PlusPlusScore.Subtract(thing, user, channel, reason);
                }
                
                if (scores.Key != null)
                {
                    if (!string.IsNullOrEmpty(reason))
                    {
                        if (scores.Value == 1 || scores.Value == -1)
                        {
                            if (scores.Key == 1 || scores.Key == -1)
                            {
                                return $"{thing} has {scores.Key} point for {reason}";
                            }
                            else
                            {
                                if (scores.Value != null)
                                {
                                    return $"{thing} has {scores.Key} points, {scores.Value} of which is for {reason}";
                                }
                            }
                        }
                        else
                        {
                            if (scores.Value != null)
                            {
                                return $"{thing} has {scores.Key} points, {scores.Value} of which are for {reason}";
                            }
                        }
                    }
                    else
                    {
                        if (scores.Key == 1)
                        {
                            return $"{thing} has {scores.Key} point";
                        }
                        else
                        {
                            return $"{thing} has {scores.Key} points";
                        }
                    }
                }
            }
            return "";
        }
    }
}