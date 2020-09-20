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
        private const string ExpectedPresidentHeader = "cycle,branch,model,modeldate,candidate_inc,candidate_chal,candidate_3rd,ecwin_inc,ecwin_chal,ecwin_3rd,ec_nomajority,popwin_inc,popwin_chal,popwin_3rd,ev_inc,ev_chal,ev_3rd,ev_inc_hi,ev_chal_hi,ev_3rd_hi,ev_inc_lo,ev_chal_lo,ev_3rd_lo,national_voteshare_inc,national_voteshare_chal,national_voteshare_3rd,nat_voteshare_other,national_voteshare_inc_hi,national_voteshare_chal_hi,national_voteshare_3rd_hi,nat_voteshare_other_hi,national_voteshare_inc_lo,national_voteshare_chal_lo,national_voteshare_3rd_lo,nat_voteshare_other_lo,national_turnout,national_turnout_hi,national_turnout_lo,timestamp,simulations";
        private const string ExpectedSenateHeader = "cycle,branch,expression,forecastdate,chamber_Dparty,chamber_Rparty,mean_seats_Dparty,mean_seats_Rparty,median_seats_Dparty,median_seats_Rparty,p90_seats_Dparty,p90_seats_Rparty,p10_seats_Dparty,p10_seats_Rparty,total_national_turnout,p90_total_national_turnout,p10_total_national_turnout,popvote_margin,p90_popvote_margin,p10_popvote_margin,simulations,timestamp";
        private const string ElectoralCollegeWinChance = "Electoral college win chance";
        private const string PopularVoteWinChance = "Popular vote win chance";
        private const string ElectoralVotes = "Electoral votes";
        private const string PopularVoteShare = "Popular vote share";
        private const string SenateWinChance = "Senate win chance";
        private const string SenateSeats = "Senate seats";
        private const string Democrats = "Democrats";
        private const string Republicans = "Republicans";

        private HttpClient httpClient;
        private static List<PresidentModelInfo> presidentModelInfo;
        private static List<SenateModelInfo> senateModelInfo;
        private DateTimeOffset lastRetrieved;

        public ElectionModelModule()
        {
            httpClient = new HttpClient();
            presidentModelInfo = new List<PresidentModelInfo>();
            senateModelInfo = new List<SenateModelInfo>();
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
                                presidentModelInfo.Clear();
                                foreach (string line in csvLines)
                                {
                                    if (string.IsNullOrEmpty(line))
                                    {
                                        continue;
                                    }
                                    if (!readHeader)
                                    {
                                        if (line != ExpectedPresidentHeader)
                                        {
                                            return;
                                        }
                                        readHeader = true;
                                    }
                                    else
                                    {
                                        presidentModelInfo.Add(new PresidentModelInfo(line));
                                    }
                                }
                            }
                        }
                        else if (entry.Name.Contains("senate_national_toplines_2020"))
                        {
                            Stream openStream = entry.Open();
                            using (StreamReader reader = new StreamReader(openStream, Encoding.UTF8))
                            {
                                string text = reader.ReadToEnd();
                                List<string> csvLines = text.Split('\n').ToList();
                                bool readHeader = false;
                                senateModelInfo.Clear();
                                foreach (string line in csvLines)
                                {
                                    if (string.IsNullOrEmpty(line))
                                    {
                                        continue;
                                    }
                                    if (!readHeader)
                                    {
                                        if (line != ExpectedSenateHeader)
                                        {
                                            return;
                                        }
                                        readHeader = true;
                                    }
                                    else
                                    {
                                        SenateModelInfo info = new SenateModelInfo(line);
                                        if (info.Expression == "_deluxe")
                                        {
                                            senateModelInfo.Add(info);
                                        }
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
            PresidentModelInfo latestPresidentInfo = presidentModelInfo.FirstOrDefault();
            if (latestPresidentInfo == null)
            {
                return null;
            }
            DateTime presidentTwoWeeksAgo = latestPresidentInfo.ModelDate - TimeSpan.FromDays(14);
            PresidentModelInfo twoWeeksAgoPresidentInfo = null;
            foreach (PresidentModelInfo info in presidentModelInfo)
            {
                if (info.ModelDate == presidentTwoWeeksAgo)
                {
                    twoWeeksAgoPresidentInfo = info;
                    break;
                }
            }
            if (twoWeeksAgoPresidentInfo == null)
            {
                return null;
            }

            bool hasThirdParty = !string.IsNullOrEmpty(latestPresidentInfo.CandidateThirdParty);

            string str = $"{latestPresidentInfo.Cycle} Presidential Election Forecast\n";
            str += $"Date: {latestPresidentInfo.ModelDate:d}\n";
            str += $"{ElectoralCollegeWinChance}: {latestPresidentInfo.CandidateIncumbent} {latestPresidentInfo.ECWinChanceIncumbent:F1} vs {latestPresidentInfo.CandidateChallenger} {latestPresidentInfo.ECWinChanceChallenger:F1}";
            if (hasThirdParty)
            {
                str += $" vs {latestPresidentInfo.CandidateThirdParty} {latestPresidentInfo.ECWinChanceThirdParty:F1}\n";
            }
            else
            {
                str += "\n";
            }
            str += $"{PopularVoteWinChance}: {latestPresidentInfo.CandidateIncumbent} {latestPresidentInfo.PopularWinChanceIncumbent:F1} vs {latestPresidentInfo.CandidateChallenger} {latestPresidentInfo.PopularWinChanceChallenger:F1}";
            if (hasThirdParty)
            {
                str += $" vs {latestPresidentInfo.CandidateThirdParty} {latestPresidentInfo.PopularWinChanceThirdParty:F1}\n";
            }
            else
            {
                str += "\n";
            }
            str += $"{ElectoralVotes}: {latestPresidentInfo.CandidateIncumbent} {latestPresidentInfo.ElectoralVotesIncumbent} vs {latestPresidentInfo.CandidateChallenger} {latestPresidentInfo.ElectoralVotesChallenger}";
            if (hasThirdParty)
            {
                str += $" vs {latestPresidentInfo.CandidateThirdParty} {latestPresidentInfo.ElectoralVotesThirdParty}\n";
            }
            else
            {
                str += "\n";
            }
            str += $"{PopularVoteShare}: {latestPresidentInfo.CandidateIncumbent} {latestPresidentInfo.PopularVoteShareIncumbent:F1} vs {latestPresidentInfo.CandidateChallenger} {latestPresidentInfo.PopularVoteShareChallenger:F1}";
            if (hasThirdParty)
            {
                str += $" vs {latestPresidentInfo.CandidateThirdParty} {latestPresidentInfo.PopularVoteShareThirdParty:F1}\n";
            }
            else
            {
                str += "\n";
            }
            str += $"\n14 days ago: {twoWeeksAgoPresidentInfo.ModelDate:d}\n";
            str += $"{ElectoralCollegeWinChance}: {twoWeeksAgoPresidentInfo.CandidateIncumbent} {twoWeeksAgoPresidentInfo.ECWinChanceIncumbent:F1} ({FormatChange(latestPresidentInfo.ECWinChanceIncumbent, twoWeeksAgoPresidentInfo.ECWinChanceIncumbent)}) vs {twoWeeksAgoPresidentInfo.CandidateChallenger} {twoWeeksAgoPresidentInfo.ECWinChanceChallenger:F1} ({FormatChange(latestPresidentInfo.ECWinChanceChallenger, twoWeeksAgoPresidentInfo.ECWinChanceChallenger)})";
            if (hasThirdParty)
            {
                str += $" vs {twoWeeksAgoPresidentInfo.CandidateThirdParty} {twoWeeksAgoPresidentInfo.ECWinChanceThirdParty:F1} ({FormatChange(latestPresidentInfo.ECWinChanceThirdParty.Value, twoWeeksAgoPresidentInfo.ECWinChanceThirdParty.Value)})\n";
            }
            else
            {
                str += "\n";
            }
            str += $"{PopularVoteWinChance}: {twoWeeksAgoPresidentInfo.CandidateIncumbent} {twoWeeksAgoPresidentInfo.PopularWinChanceIncumbent:F1} ({FormatChange(latestPresidentInfo.PopularWinChanceIncumbent, twoWeeksAgoPresidentInfo.PopularWinChanceIncumbent)}) vs {twoWeeksAgoPresidentInfo.CandidateChallenger} {twoWeeksAgoPresidentInfo.PopularWinChanceChallenger:F1} ({FormatChange(latestPresidentInfo.PopularWinChanceChallenger, twoWeeksAgoPresidentInfo.PopularWinChanceChallenger)})";
            if (hasThirdParty)
            {
                str += $" vs {twoWeeksAgoPresidentInfo.CandidateThirdParty} {twoWeeksAgoPresidentInfo.PopularWinChanceThirdParty:F1} ({FormatChange(latestPresidentInfo.PopularWinChanceThirdParty.Value, twoWeeksAgoPresidentInfo.PopularWinChanceThirdParty.Value)})\n";
            }
            else
            {
                str += "\n";
            }
            str += $"{ElectoralVotes}: {twoWeeksAgoPresidentInfo.CandidateIncumbent} {twoWeeksAgoPresidentInfo.ElectoralVotesIncumbent} ({FormatChange(latestPresidentInfo.ElectoralVotesIncumbent, twoWeeksAgoPresidentInfo.ElectoralVotesIncumbent)}) vs {twoWeeksAgoPresidentInfo.CandidateChallenger} {twoWeeksAgoPresidentInfo.ElectoralVotesChallenger} ({FormatChange(latestPresidentInfo.ElectoralVotesChallenger, twoWeeksAgoPresidentInfo.ElectoralVotesChallenger)})";
            if (hasThirdParty)
            {
                str += $" vs {twoWeeksAgoPresidentInfo.CandidateThirdParty} {twoWeeksAgoPresidentInfo.ElectoralVotesThirdParty} ({FormatChange(latestPresidentInfo.ElectoralVotesThirdParty.Value, twoWeeksAgoPresidentInfo.ElectoralVotesThirdParty.Value)})\n";
            }
            else
            {
                str += "\n";
            }
            str += $"{PopularVoteShare}: {twoWeeksAgoPresidentInfo.CandidateIncumbent} {twoWeeksAgoPresidentInfo.PopularVoteShareIncumbent:F1} ({FormatChange(latestPresidentInfo.PopularVoteShareIncumbent, twoWeeksAgoPresidentInfo.PopularVoteShareIncumbent)}) vs {twoWeeksAgoPresidentInfo.CandidateChallenger} {twoWeeksAgoPresidentInfo.PopularVoteShareChallenger:F1} ({FormatChange(latestPresidentInfo.PopularVoteShareChallenger, twoWeeksAgoPresidentInfo.PopularVoteShareChallenger)})";
            if (hasThirdParty)
            {
                str += $" vs {twoWeeksAgoPresidentInfo.CandidateThirdParty} {twoWeeksAgoPresidentInfo.PopularVoteShareThirdParty:F1} ({FormatChange(latestPresidentInfo.PopularVoteShareThirdParty.Value, twoWeeksAgoPresidentInfo.PopularVoteShareThirdParty.Value)})\n";
            }
            else
            {
                str += "\n";
            }
            str += $"\nModel run timestamp: {latestPresidentInfo.Timestamp:s}\n";

            SenateModelInfo latestSenateInfo = senateModelInfo.FirstOrDefault();
            if (latestSenateInfo == null)
            {
                str += $"\nLast retrieved: {lastRetrieved:s}";
                return str;
            }
            DateTime senateTwoWeeksAgo = latestSenateInfo.ForecastDate - TimeSpan.FromDays(14);
            SenateModelInfo twoWeeksAgoSenateInfo = null;
            foreach (SenateModelInfo info in senateModelInfo)
            {
                if (info.ForecastDate == senateTwoWeeksAgo)
                {
                    twoWeeksAgoSenateInfo = info;
                    break;
                }
            }
            if (twoWeeksAgoSenateInfo == null)
            {
                str += $"\nLast retrieved: {lastRetrieved:s}";
                return str;
            }

            str += $"\n\n{latestSenateInfo.Cycle} Senate Election Forecast\n";
            str += $"Date: {latestSenateInfo.ForecastDate:d}\n";
            str += $"{SenateWinChance}: {Republicans} {latestSenateInfo.ChamberRParty:F1} vs {Democrats} {latestSenateInfo.ChamberDParty:F1}\n";
            str += $"{SenateSeats}: {Republicans} {(int)Math.Round(latestSenateInfo.MeanSeatsRParty)} vs {Democrats} {(int)Math.Round(latestSenateInfo.MeanSeatsDParty)}\n";
            str += $"\n14 days ago: {twoWeeksAgoSenateInfo.ForecastDate:d}\n";
            str += $"{SenateWinChance}: {Republicans} {twoWeeksAgoSenateInfo.ChamberRParty:F1} ({FormatChange(latestSenateInfo.ChamberRParty, twoWeeksAgoSenateInfo.ChamberRParty)}) vs {Democrats} {twoWeeksAgoSenateInfo.ChamberDParty:F1} ({FormatChange(latestSenateInfo.ChamberDParty, twoWeeksAgoSenateInfo.ChamberDParty)})\n";
            str += $"{SenateSeats}: {Republicans} {(int)Math.Round(twoWeeksAgoSenateInfo.MeanSeatsRParty)} ({FormatChange(Math.Round(latestSenateInfo.MeanSeatsRParty), Math.Round(twoWeeksAgoSenateInfo.MeanSeatsRParty))}) vs {Democrats} {(int)Math.Round(twoWeeksAgoSenateInfo.MeanSeatsDParty)} ({FormatChange(Math.Round(latestSenateInfo.MeanSeatsDParty), Math.Round(twoWeeksAgoSenateInfo.MeanSeatsDParty))})\n";
            str += $"\n\nModel run timestamp: {latestSenateInfo.Timestamp:s}\n";
            str += $"\nLast retrieved: {lastRetrieved:s}";
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

        private class PresidentModelInfo
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

            public PresidentModelInfo(string line)
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
                Timestamp = DateTime.ParseExact(splitLine[38], new string[] { "HH:mm:ss d MMM yyyy", "HH:mm:ss  d MMM yyyy" }, CultureInfo.InvariantCulture);
            }
        }

        private class SenateModelInfo
        {
            public string Cycle { get; private set; }
            public string Branch { get; private set; }
            public string Expression { get; private set; }
            public DateTime ForecastDate { get; private set; }
            public double ChamberDParty { get; private set; }
            public double ChamberRParty { get; private set; }
            public double MeanSeatsDParty { get; private set; }
            public double MeanSeatsRParty { get; private set; }
            public DateTime Timestamp { get; private set; }

            public SenateModelInfo(string line)
            {
                string[] splitLine = line.Split(',');
                Cycle = splitLine[0];
                Branch = splitLine[1];
                Expression = splitLine[2];
                ForecastDate = DateTime.ParseExact(splitLine[3], "M/d/yy", CultureInfo.InvariantCulture);
                ChamberDParty = double.Parse(splitLine[4]) * 100;
                ChamberRParty = double.Parse(splitLine[5]) * 100;
                MeanSeatsDParty = double.Parse(splitLine[6]);
                MeanSeatsRParty = double.Parse(splitLine[7]);
                Timestamp = DateTime.ParseExact(splitLine[21], new string[] { "HH:mm:ss d MMM yyyy", "HH:mm:ss  d MMM yyyy" }, CultureInfo.InvariantCulture);
            }
        }
    }
}
