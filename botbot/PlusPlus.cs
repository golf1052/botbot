using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using MongoDB.Driver;
using System.Text;

namespace botbot
{
    public class PlusPlus
    {
        // PlusPlus was entirely copied from here https://github.com/ajacksified/hubot-plusplus
        // so I don't know how sweet this regex is...bro
        const string plusPlusPattern = "^([\\s\\w'@.\\-:\u3040-\u30FF\uFF01-\uFF60\u4E00-\u9FA0]*)\\s*(\\+\\+|--|—)(?:\\s+(?:for|because|cause|cuz|as)\\s+(.+))?$";
        const string erasePattern = "(?:erase )([\\s\\w'@.-:\u3040-\u30FF\uFF01-\uFF60\u4E00-\u9FA0]*)(?:\\s+(?:for|because|cause|cuz)\\s+(.+))?$";
        const string scorePattern = "(?:score) (for\\s)?(.*)";
        const string topBottomPattern = "(top|bottom) (\\d+)";

        public static string CheckPlusPlus(string message, string channel, string user)
        {
            Regex regex = new Regex(plusPlusPattern);
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
                        return $"{thing} has {scores.Key} {(scores.Key == 1 || scores.Key == -1 ? "point" : "points")}";
                    }
                }
            }
            return string.Empty;
        }

        public static string CheckErase(string message, string channel, string user)
        {
            Regex regex = new Regex(erasePattern);
            Match match = regex.Match(message);
            if (match.Success)
            {
                string thing = match.Groups[1].Value;
                string reason = match.Groups[2].Value;
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
                return "https://www.youtube.com/watch?v=ARJ8cAGm6JE&t=1m3s";
            }
            return string.Empty;
        }

        public static string CheckScore(string message, string channel, string user)
        {
            Regex regex = new Regex(scorePattern);
            Match match = regex.Match(message);
            if (match.Success)
            {
                string thing = match.Groups[2].Value;
                thing = thing.Trim().ToLower();
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

                int score = PlusPlusScore.ScoreForThing(thing);
                Dictionary<string, int> reasons = PlusPlusScore.ReasonsForThing(thing);

                string reasonString = string.Empty;
                if (reasons.Count == 0)
                {
                    return $"{thing} has {score} {(score == 1 || score == -1 ? "point" : "points")}.";
                }
                else
                {
                    string memo = string.Empty;
                    return $"{thing} has {score} {(score == 1 || score == -1 ? "point" : "points")}. Here are some reasons:" +
                        reasons.Aggregate(new StringBuilder(), (sb, r) =>
                        {
                            sb.Append($"\n{r.Key}: {r.Value} {(r.Value == 1 || r.Value == -1 ? "point" : "points")}");
                            return sb;
                        }).ToString();
                }
            }
            return "";
        }

        public static string CheckTopBottom(string message, string channel, string user)
        {
            Regex regex = new Regex(topBottomPattern);
            Match match = regex.Match(message);
            if (match.Success)
            {
                string topOrBottom = match.Groups[1].Value;
                topOrBottom = topOrBottom.Trim().ToLower();
                string amountString = match.Groups[2].Value;
                int amount = 10;
                try
                {
                    amount = int.Parse(amountString);
                }
                catch (Exception)
                {
                }

                string reply = string.Empty;
                List<KeyValuePair<string, int>> topBottom = new List<KeyValuePair<string, int>>();
                if (topOrBottom == "top")
                {
                    topBottom = PlusPlusScore.Top(amount);
                }
                else if (topOrBottom == "bottom")
                {
                    topBottom = PlusPlusScore.Bottom(amount);
                }
                if (topBottom.Count > 0)
                {
                    for (int i = 0; i < topBottom.Count; i++)
                    {
                        reply += $"#{i + 1}. {topBottom[i].Key} : {topBottom[i].Value}\n";
                    }
                }
                else
                {
                    reply += "No scores to keep track of yet!";
                }
                return reply;
            }
            return string.Empty;
        }
    }
}
