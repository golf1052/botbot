using Reverb.Models.WebPlayer;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace botbot.Module
{
    public class ElectionModelModule : IMessageModule
    {
        private const string ModelResultsZIP = "https://projects.fivethirtyeight.com/data-webpage-data/datasets/election-forecasts-2020.zip";
        private const string ExpectedHeader = "cycle,branch,model,modeldate,candidate_inc,candidate_chal,candidate_3rd,ecwin_inc,ecwin_chal,ecwin_3rd,ec_nomajority,popwin_inc,popwin_chal,popwin_3rd,ev_inc,ev_chal,ev_3rd,ev_inc_hi,ev_chal_hi,ev_3rd_hi,ev_inc_lo,ev_chal_lo,ev_3rd_lo,national_voteshare_inc,national_voteshare_chal,national_voteshare_3rd,nat_voteshare_other,national_voteshare_inc_hi,national_voteshare_chal_hi,national_voteshare_3rd_hi,nat_voteshare_other_hi,national_voteshare_inc_lo,national_voteshare_chal_lo,national_voteshare_3rd_lo,nat_voteshare_other_lo,timestamp,simulations";
        private const string ElectoralCollegeWinChance = "Electoral college win chance";
        private const string PopularVoteWinChance = "Popular vote win chance";
        private const string ElectoralVotes = "Electoral votes";
        private const string PopularVoteShare = "Popular vote share";

        private HttpClient httpClient;
        private static List<ModelInfo> modelInfo;
        private DateTimeOffset lastRetrieved;

        public ElectionModelModule()
        {
            httpClient = new HttpClient();
            modelInfo = new List<ModelInfo>();
            lastRetrieved = DateTimeOffset.UnixEpoch;
        }
        
        public async Task<string> Handle(string text, string userId, string channel)
        {
            if (text.ToLower().StartsWith("botbot election"))
            {
                await RetrieveLatestModel();
                return GetLatestForecast();
            }
            return null;
        }

        private async Task RetrieveLatestModel()
        {
            if (lastRetrieved < DateTimeOffset.UtcNow - TimeSpan.FromHours(6))
            {
                lastRetrieved = DateTimeOffset.UtcNow;
                HttpResponseMessage responseMessage = await httpClient.GetAsync(ModelResultsZIP);
                if (!responseMessage.IsSuccessStatusCode)
                {
                    return;
                }

                Stream stream = await responseMessage.Content.ReadAsStreamAsync();
                using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (entry.Name.Contains("presidential_national_toplines_2020"))
                        {
                            Stream openStream = entry.Open();
                            using (StreamReader reader = new StreamReader(openStream, Encoding.UTF8))
                            {
                                string text = reader.ReadToEnd();
                                List<string> csvLines = text.Split('\n').ToList();
                                bool readHeader = false;
                                modelInfo.Clear();
                                foreach (string line in csvLines)
                                {
                                    if (string.IsNullOrEmpty(line))
                                    {
                                        continue;
                                    }
                                    if (!readHeader)
                                    {
                                        if (line != ExpectedHeader)
                                        {
                                            return;
                                        }
                                        readHeader = true;
                                    }
                                    else
                                    {
                                        modelInfo.Add(new ModelInfo(line));
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private string GetLatestForecast()
        {
            ModelInfo latestInfo = modelInfo.FirstOrDefault();
            if (latestInfo == null)
            {
                return null;
            }
            DateTime twoWeeksAgo = latestInfo.ModelDate - TimeSpan.FromDays(14);
            ModelInfo twoWeeksAgoInfo = null;
            foreach (ModelInfo info in modelInfo)
            {
                if (info.ModelDate == twoWeeksAgo)
                {
                    twoWeeksAgoInfo = info;
                    break;
                }
            }
            if (twoWeeksAgoInfo == null)
            {
                return null;
            }

            bool hasThirdParty = !string.IsNullOrEmpty(latestInfo.CandidateThirdParty);

            string str = $"{latestInfo.Cycle} Election Forecast\n";
            str += $"Date: {latestInfo.ModelDate:d}\n";
            str += $"{ElectoralCollegeWinChance}: {latestInfo.CandidateIncumbent} {latestInfo.ECWinChanceIncumbent:F1} vs {latestInfo.CandidateChallenger} {latestInfo.ECWinChanceChallenger:F1}";
            if (hasThirdParty)
            {
                str += $" vs {latestInfo.CandidateThirdParty} {latestInfo.ECWinChanceThirdParty:F1}\n";
            }
            else
            {
                str += "\n";
            }
            str += $"{PopularVoteWinChance}: {latestInfo.CandidateIncumbent} {latestInfo.PopularWinChanceIncumbent:F1} vs {latestInfo.CandidateChallenger} {latestInfo.PopularWinChanceChallenger:F1}";
            if (hasThirdParty)
            {
                str += $" vs {latestInfo.CandidateThirdParty} {latestInfo.PopularWinChanceThirdParty:F1}\n";
            }
            else
            {
                str += "\n";
            }
            str += $"{ElectoralVotes}: {latestInfo.CandidateIncumbent} {latestInfo.ElectoralVotesIncumbent} vs {latestInfo.CandidateChallenger} {latestInfo.ElectoralVotesChallenger}";
            if (hasThirdParty)
            {
                str += $" vs {latestInfo.CandidateThirdParty} {latestInfo.ElectoralVotesThirdParty}\n";
            }
            else
            {
                str += "\n";
            }
            str += $"{PopularVoteShare}: {latestInfo.CandidateIncumbent} {latestInfo.PopularVoteShareIncumbent:F1} vs {latestInfo.CandidateChallenger} {latestInfo.PopularVoteShareChallenger:F1}";
            if (hasThirdParty)
            {
                str += $" vs {latestInfo.CandidateThirdParty} {latestInfo.PopularVoteShareThirdParty:F1}\n";
            }
            else
            {
                str += "\n";
            }
            str += $"\n14 days ago: {twoWeeksAgoInfo.ModelDate:d}\n";
            str += $"{ElectoralCollegeWinChance}: {twoWeeksAgoInfo.CandidateIncumbent} {twoWeeksAgoInfo.ECWinChanceIncumbent:F1} ({FormatChange(latestInfo.ECWinChanceIncumbent, twoWeeksAgoInfo.ECWinChanceIncumbent)}) vs {twoWeeksAgoInfo.CandidateChallenger} {twoWeeksAgoInfo.ECWinChanceChallenger:F1} ({FormatChange(latestInfo.ECWinChanceChallenger, twoWeeksAgoInfo.ECWinChanceChallenger)})";
            if (hasThirdParty)
            {
                str += $" vs {twoWeeksAgoInfo.CandidateThirdParty} {twoWeeksAgoInfo.ECWinChanceThirdParty:F1} ({FormatChange(latestInfo.ECWinChanceThirdParty.Value, twoWeeksAgoInfo.ECWinChanceThirdParty.Value)})\n";
            }
            else
            {
                str += "\n";
            }
            str += $"{PopularVoteWinChance}: {twoWeeksAgoInfo.CandidateIncumbent} {twoWeeksAgoInfo.PopularWinChanceIncumbent:F1} ({FormatChange(latestInfo.PopularWinChanceIncumbent, twoWeeksAgoInfo.PopularWinChanceIncumbent)}) vs {twoWeeksAgoInfo.CandidateChallenger} {twoWeeksAgoInfo.PopularWinChanceChallenger:F1} ({FormatChange(latestInfo.PopularWinChanceChallenger, twoWeeksAgoInfo.PopularWinChanceChallenger)})";
            if (hasThirdParty)
            {
                str += $" vs {twoWeeksAgoInfo.CandidateThirdParty} {twoWeeksAgoInfo.PopularWinChanceThirdParty:F1} ({FormatChange(latestInfo.PopularWinChanceThirdParty.Value, twoWeeksAgoInfo.PopularWinChanceThirdParty.Value)})\n";
            }
            else
            {
                str += "\n";
            }
            str += $"{ElectoralVotes}: {twoWeeksAgoInfo.CandidateIncumbent} {twoWeeksAgoInfo.ElectoralVotesIncumbent} ({FormatChange(latestInfo.ElectoralVotesIncumbent, twoWeeksAgoInfo.ElectoralVotesIncumbent)}) vs {twoWeeksAgoInfo.CandidateChallenger} {twoWeeksAgoInfo.ElectoralVotesChallenger} ({FormatChange(latestInfo.ElectoralVotesChallenger, twoWeeksAgoInfo.ElectoralVotesChallenger)})";
            if (hasThirdParty)
            {
                str += $" vs {twoWeeksAgoInfo.CandidateThirdParty} {twoWeeksAgoInfo.ElectoralVotesThirdParty} ({FormatChange(latestInfo.ElectoralVotesThirdParty.Value, twoWeeksAgoInfo.ElectoralVotesThirdParty.Value)})\n";
            }
            else
            {
                str += "\n";
            }
            str += $"{PopularVoteShare}: {twoWeeksAgoInfo.CandidateIncumbent} {twoWeeksAgoInfo.PopularVoteShareIncumbent:F1} ({FormatChange(latestInfo.PopularVoteShareIncumbent, twoWeeksAgoInfo.PopularVoteShareIncumbent)}) vs {twoWeeksAgoInfo.CandidateChallenger} {twoWeeksAgoInfo.PopularVoteShareChallenger:F1} ({FormatChange(latestInfo.PopularVoteShareChallenger, twoWeeksAgoInfo.PopularVoteShareChallenger)})";
            if (hasThirdParty)
            {
                str += $" vs {twoWeeksAgoInfo.CandidateThirdParty} {twoWeeksAgoInfo.PopularVoteShareThirdParty:F1} ({FormatChange(latestInfo.PopularVoteShareThirdParty.Value, twoWeeksAgoInfo.PopularVoteShareThirdParty.Value)})\n";
            }
            else
            {
                str += "\n";
            }
            str += $"\nModel run timestamp: {latestInfo.Timestamp:s}\n";
            str += $"Last retrieved: {lastRetrieved:s}";
            return str;
        }

        private string FormatChange(double current, double previous)
        {
            double change = current - previous;
            if (change > 0)
            {
                return $"🔼 {change:F1}";
            }
            else if (change < 0)
            {
                return $"🔽 {change:F1}";
            }
            else
            {
                return $"{change:F1}";
            }
        }

        private class ModelInfo
        {
            public string Cycle { get; private set; }
            public string Branch { get; private set; }
            public string Model { get; private set; }
            public DateTime ModelDate { get; private set; }
            public string CandidateIncumbent { get; private set; }
            public string CandidateChallenger { get; private set; }
            public string CandidateThirdParty { get; private set; }
            public double ECWinChanceIncumbent { get; private set; }
            public double ECWinChanceChallenger { get; private set; }
            public double? ECWinChanceThirdParty { get; private set; }
            public double ECNoMajority { get; private set; }
            public double PopularWinChanceIncumbent { get; private set; }
            public double PopularWinChanceChallenger { get; private set; }
            public double? PopularWinChanceThirdParty { get; private set; }
            public int ElectoralVotesIncumbent { get; private set; }
            public int ElectoralVotesChallenger { get; private set; }
            public int? ElectoralVotesThirdParty { get; private set; }
            public double PopularVoteShareIncumbent { get; private set; }
            public double PopularVoteShareChallenger { get; private set; }
            public double? PopularVoteShareThirdParty { get; private set; }
            public double PopularVoteShareOther { get; private set; }
            public DateTime Timestamp { get; private set; }

            public ModelInfo(string line)
            {
                string[] splitLine = line.Split(',');
                Cycle = splitLine[0];
                Branch = splitLine[1];
                Model = splitLine[2];
                ModelDate = DateTime.ParseExact(splitLine[3], "M/d/yyyy", CultureInfo.InvariantCulture);
                CandidateIncumbent = splitLine[4];
                CandidateChallenger = splitLine[5];
                CandidateThirdParty = splitLine[6] == "\"\"" ? null : splitLine[6];
                ECWinChanceIncumbent = double.Parse(splitLine[7]) * 100;
                ECWinChanceChallenger = double.Parse(splitLine[8]) * 100;
                if (double.TryParse(splitLine[9], out double ecwin_3rd))
                {
                    ECWinChanceThirdParty = ecwin_3rd * 100;
                }
                else
                {
                    ECWinChanceThirdParty = null;
                }
                ECNoMajority = double.Parse(splitLine[10]) * 100;
                PopularWinChanceIncumbent = double.Parse(splitLine[11]) * 100;
                PopularWinChanceChallenger = double.Parse(splitLine[12]) * 100;
                if (double.TryParse(splitLine[13], out double popwin_3rd))
                {
                    PopularWinChanceThirdParty = popwin_3rd * 100;
                }
                else
                {
                    PopularWinChanceThirdParty = null;
                }
                ElectoralVotesIncumbent = (int)Math.Round(double.Parse(splitLine[14]));
                ElectoralVotesChallenger = (int)Math.Round(double.Parse(splitLine[15]));
                if (double.TryParse(splitLine[16], out double ev_3rd))
                {
                    ElectoralVotesThirdParty = (int)Math.Round(ev_3rd);
                }
                else
                {
                    ElectoralVotesThirdParty = null;
                }
                PopularVoteShareIncumbent = double.Parse(splitLine[23]);
                PopularVoteShareChallenger = double.Parse(splitLine[24]);
                if (double.TryParse(splitLine[25], out double national_voteshare_3rd))
                {
                    PopularVoteShareThirdParty = national_voteshare_3rd;
                }
                else
                {
                    PopularVoteShareThirdParty = null;
                }
                PopularVoteShareOther = double.Parse(splitLine[26]);
                Timestamp = DateTime.ParseExact(splitLine[35], new string[] { "HH:mm:ss d MMM yyyy", "HH:mm:ss  d MMM yyyy" }, CultureInfo.InvariantCulture);
            }
        }
    }
}
