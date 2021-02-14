using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace botbot.Command
{
    public class PresidentApprovalCommand : ISlackCommand
    {
        private const string ApprovalCSV = "https://projects.fivethirtyeight.com/biden-approval-data/approval_topline.csv";
        private const string ExpectedHeader = "president,subgroup,modeldate,approve_estimate,approve_hi,approve_lo,disapprove_estimate,disapprove_hi,disapprove_lo,timestamp";
        private readonly HttpClient httpClient;
        private static readonly List<ApprovalInfo> approvalInfo;
        private DateTimeOffset lastRetrieved;

        static PresidentApprovalCommand()
        {
            approvalInfo = new List<ApprovalInfo>();
        }

        public PresidentApprovalCommand()
        {
            httpClient = new HttpClient();
            lastRetrieved = DateTimeOffset.UnixEpoch;
        }

        public async Task<string> Handle(string text, string userId)
        {
            await RetrieveLatestApproval();
            return GetLatestApproval();
        }

        private async Task RetrieveLatestApproval()
        {
            if (lastRetrieved < DateTimeOffset.UtcNow - TimeSpan.FromHours(6))
            {
                lastRetrieved = DateTimeOffset.UtcNow;
                HttpResponseMessage responseMessage = await httpClient.GetAsync(ApprovalCSV);
                if (!responseMessage.IsSuccessStatusCode)
                {
                    return;
                }
                string csvString = await responseMessage.Content.ReadAsStringAsync();
                List<string> csvLines = csvString.Split('\n').ToList();
                bool readHeader = false;
                approvalInfo.Clear();
                foreach (string line in csvLines)
                {
                    if (string.IsNullOrWhiteSpace(line))
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
                        approvalInfo.Add(new ApprovalInfo(line));
                    }
                }
            }
        }

        private string GetLatestApproval()
        {
            ApprovalInfo latestInfo = approvalInfo.First(a => a.Subgroup == "All polls");
            DateTime latestModel = latestInfo.ModelDate;
            DateTime monthWindowDate = latestModel - TimeSpan.FromDays(30);
            List<ApprovalInfo> approvalInfoRange = new List<ApprovalInfo>();
            foreach (ApprovalInfo info in approvalInfo)
            {
                if (info.ModelDate >= monthWindowDate)
                {
                    if (info.Subgroup == "All polls")
                    {
                        approvalInfoRange.Add(info);
                    }
                }
                else
                {
                    break;
                }
            }
            string str = $"{latestInfo.President} approval ratings\n";
            ApprovalInfo oldestRangeInfo = approvalInfoRange[approvalInfoRange.Count - 1];
            TimeSpan range = latestModel - oldestRangeInfo.ModelDate;
            double approvalChange = latestInfo.ApprovalEstimate - oldestRangeInfo.ApprovalEstimate;
            double disapprovalChange = latestInfo.DisapprovalEstimate - oldestRangeInfo.DisapprovalEstimate;
            ApprovalInfo maxApproval = latestInfo;
            ApprovalInfo minApproval = latestInfo;
            ApprovalInfo maxDisapproval = latestInfo;
            ApprovalInfo minDisapproval = latestInfo;
            foreach (ApprovalInfo info in approvalInfoRange)
            {
                if (info.ApprovalEstimate > maxApproval.ApprovalEstimate)
                {
                    maxApproval = info;
                }
                if (info.ApprovalEstimate < minApproval.ApprovalEstimate)
                {
                    minApproval = info;
                }
                if (info.DisapprovalEstimate > maxDisapproval.DisapprovalEstimate)
                {
                    maxDisapproval = info;
                }
                if (info.DisapprovalEstimate < minDisapproval.DisapprovalEstimate)
                {
                    minDisapproval = info;
                }
            }

            if (latestInfo.ApprovalEstimate >= latestInfo.DisapprovalEstimate)
            {
                str += $"Today's approval: {latestInfo.ApprovalEstimate:F1}\nToday's disapproval: {latestInfo.DisapprovalEstimate:F1}\n";
                str += $"Approval {range.TotalDays} days ago: {oldestRangeInfo.ApprovalEstimate:F1} ({FormatChange(latestInfo.ApprovalEstimate, oldestRangeInfo.ApprovalEstimate)})\nDisapproval {range.TotalDays} days ago: {oldestRangeInfo.DisapprovalEstimate:F1} ({FormatChange(latestInfo.DisapprovalEstimate, oldestRangeInfo.DisapprovalEstimate)})\n";
                str += $"Approval range for {range.TotalDays} days: {FormatChange(latestInfo.ApprovalEstimate, maxApproval.ApprovalEstimate)} from high ({maxApproval.ApprovalEstimate:F1}), {FormatChange(latestInfo.ApprovalEstimate, minApproval.ApprovalEstimate)} from low ({minApproval.ApprovalEstimate:F1})\n";
                str += $"Disapproval range for {range.TotalDays} days: {FormatChange(latestInfo.DisapprovalEstimate, maxDisapproval.DisapprovalEstimate)} from high ({maxDisapproval.DisapprovalEstimate:F1}), {FormatChange(latestInfo.DisapprovalEstimate, minDisapproval.DisapprovalEstimate)} from low ({minDisapproval.DisapprovalEstimate:F1})";
            }
            else
            {
                str += $"Today's disapproval: {latestInfo.DisapprovalEstimate:F1}\nToday's approval: {latestInfo.ApprovalEstimate:F1}\n";
                str += $"Disapproval {range.TotalDays} days ago: {oldestRangeInfo.DisapprovalEstimate:F1} ({FormatChange(latestInfo.DisapprovalEstimate, oldestRangeInfo.DisapprovalEstimate)})\nApproval {range.TotalDays} days ago: {oldestRangeInfo.ApprovalEstimate:F1} ({FormatChange(latestInfo.ApprovalEstimate, oldestRangeInfo.ApprovalEstimate)})\n";
                str += $"Disapproval range for {range.TotalDays} days: {FormatChange(latestInfo.DisapprovalEstimate, maxDisapproval.DisapprovalEstimate)} from high ({maxDisapproval.DisapprovalEstimate:F1}), {FormatChange(latestInfo.DisapprovalEstimate, minDisapproval.DisapprovalEstimate)} from low ({minDisapproval.DisapprovalEstimate:F1})\n";
                str += $"Approval range for {range.TotalDays} days: {FormatChange(latestInfo.ApprovalEstimate, maxApproval.ApprovalEstimate)} from high ({maxApproval.ApprovalEstimate:F1}), {FormatChange(latestInfo.ApprovalEstimate, minApproval.ApprovalEstimate)} from low ({minApproval.ApprovalEstimate:F1})\n";
            }
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

        private class ApprovalInfo
        {
            public string President { get; private set; }
            public string Subgroup { get; private set; }
            public DateTime ModelDate { get; private set; }
            public double ApprovalEstimate { get; private set; }
            public double ApprovalHigh { get; private set; }
            public double ApprovalLow { get; private set; }
            public double DisapprovalEstimate { get; private set; }
            public double DisapprovalHigh { get; private set; }
            public double DisapprovalLow { get; private set; }
            public DateTime Timestamp { get; private set; }

            public ApprovalInfo(string line)
            {
                string[] splitLine = line.Split(',');
                President = splitLine[0];
                Subgroup = splitLine[1];
                ModelDate = DateTime.ParseExact(splitLine[2], "M/d/yyyy", CultureInfo.InvariantCulture);
                ApprovalEstimate = double.Parse(splitLine[3]);
                ApprovalHigh = double.Parse(splitLine[4]);
                ApprovalLow = double.Parse(splitLine[5]);
                DisapprovalEstimate = double.Parse(splitLine[6]);
                DisapprovalHigh = double.Parse(splitLine[7]);
                DisapprovalLow = double.Parse(splitLine[8]);
                Timestamp = DateTime.ParseExact(splitLine[9], new string[] { "HH:mm:ss d MMM yyyy", "HH:mm:ss  d MMM yyyy" }, CultureInfo.InvariantCulture);
            }
        }
    }
}
