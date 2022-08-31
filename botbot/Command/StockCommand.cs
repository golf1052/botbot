using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using botbot.Command.Stock;
using Flurl;
using golf1052.SlackAPI.BlockKit.Blocks;
using IEXSharp;
using IEXSharp.Helper;
using IEXSharp.Model.CoreData.StockPrices.Request;
using IEXSharp.Model.Shared.Response;
using Newtonsoft.Json;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.ImageSharp;
using OxyPlot.Series;
using ThreeFourteen.AlphaVantage;

namespace botbot.Command
{
    public class StockCommand : ISlackCommand
    {
        private const string BaseUrl = "https://www.alphavantage.co/query";

        private readonly AlphaVantage alphaVantageClient;
        private readonly IEXCloudClient iexClient;
        private readonly HttpClient httpClient;

        public StockCommand()
        {
            string publishableToken;
            string secretToken;
            bool useSandbox = false;

            if (!useSandbox)
            {
                publishableToken = Secrets.IEXPublishableToken;
                secretToken = Secrets.IEXSecretToken;
            }
            else
            {
                publishableToken = Secrets.IEXSandboxPublishableToken;
                secretToken = Secrets.IEXSandboxSecretToken;
            }

            iexClient = new IEXCloudClient(publishableToken, secretToken, false, useSandbox);
            alphaVantageClient = new AlphaVantage(Secrets.AlphaVantageApiKey);
            httpClient = new HttpClient();
        }

        private async Task<string> Historical(string stock, string range)
        {
            string? filename = GetHistoricalImage(stock, range);
            if (filename != null)
            {
                return filename;
            }
            QueryStringBuilder qsb = new QueryStringBuilder();
            qsb.Add("chartCloseOnly", "true");
            qsb.Add("chartSimplify", "true");

            DateTimeAxis dateTimeAxis = new DateTimeAxis()
            {
                Position = AxisPosition.Bottom,
                IntervalType = DateTimeIntervalType.Days,
                Angle = -45
            };

            ChartRange chartRange = ChartRange.OneMonth;
            if (range == "max")
            {
                chartRange = ChartRange.Max;
                dateTimeAxis.IntervalType = DateTimeIntervalType.Months;
            }
            else if (range == "5y")
            {
                chartRange = ChartRange.FiveYears;
                dateTimeAxis.IntervalType = DateTimeIntervalType.Months;
            }
            else if (range == "2y")
            {
                chartRange = ChartRange.TwoYears;
                dateTimeAxis.IntervalType = DateTimeIntervalType.Months;
            }
            else if (range == "1y")
            {
                chartRange = ChartRange.OneYear;
            }
            else if (range == "ytd")
            {
                chartRange = ChartRange.Ytd;
            }
            else if (range == "6m")
            {
                chartRange = ChartRange.SixMonths;
            }
            else if (range == "3m")
            {
                chartRange = ChartRange.ThreeMonths;
            }
            else if (range == "1m")
            {
                chartRange = ChartRange.OneMonth;
            }
            else if (range == "5d")
            {
                chartRange = ChartRange.FiveDay;
            }
            var result = await iexClient.StockPrices.HistoricalPriceAsync(stock, chartRange, qsb);
            if (result.ErrorMessage != null)
            {
                throw new Exception(result.ErrorMessage);
            }

            double dateMin = double.MaxValue;
            double dateMax = 0;
            double valMin = double.MaxValue;
            double valMax = 0;
            LineSeries lineSeries = new LineSeries();
            lineSeries.StrokeThickness = 4;
            foreach (var r in result.Data)
            {
                double closeValue = (double)r.close!.Value;
                DateTime date = DateTime.Parse(r.date);
                if (DateTimeAxis.ToDouble(date) > dateMax)
                {
                    dateMax = DateTimeAxis.ToDouble(date);
                }
                if (DateTimeAxis.ToDouble(date) < dateMin)
                {
                    dateMin = DateTimeAxis.ToDouble(date);
                }
                if (closeValue > valMax)
                {
                    valMax = closeValue;
                }
                if (closeValue < valMin)
                {
                    valMin = closeValue;
                }
                lineSeries.Points.Add(DateTimeAxis.CreateDataPoint(date, closeValue));
            }
            //dateTimeAxis.Minimum = dateMin;
            //dateTimeAxis.Maximum = dateMax;
            dateTimeAxis.MajorGridlineStyle = LineStyle.Solid;
            dateTimeAxis.MinorGridlineStyle = LineStyle.Solid;
            dateTimeAxis.StringFormat = "yyyy-MM-dd";

            PlotModel plotModel = new PlotModel()
            {
                Title = $"{stock} {range}",
                TitleFontSize = 28,
                DefaultFontSize = 28
            };
            plotModel.Series.Add(lineSeries);
            plotModel.Axes.Add(dateTimeAxis);
            plotModel.Axes.Add(new LinearAxis()
            {
                Position = AxisPosition.Left,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Solid
                //Minimum = valMin,
                //Maximum = valMax
            });

            plotModel.Background = OxyColors.White;

            filename = $"{stock}_{range}_{DateTime.Now:yyyy-MM-dd}.jpg";
            using (var stream = new MemoryStream())
            {
                var jpegExporter = new JpegExporter(1920, 1080);
                jpegExporter.Export(plotModel, stream);
                System.IO.File.WriteAllBytes($"../../images/{filename}", stream.ToArray());
            }
            return filename;
        }

        private string? GetHistoricalImage(string stock, string range)
        {
            string filename = $"{stock}_{range}_{DateTime.Now:yyyy-MM-dd}.jpg";
            string path = $"../../images/{filename}";
            if (System.IO.File.Exists(path))
            {
                return filename;
            }
            else
            {
                return null;
            }
        }

        public async Task<string> Handle(string text, string userId)
        {
            return null;
        }

        public async Task<List<IBlock>?> HandleBlock(string text, string userId)
        {
            List<IBlock> result = new List<IBlock>();
            if (string.IsNullOrWhiteSpace(text))
            {
                result.Add(new Section("No stock specified"));
                return result;
            }

            if (text.StartsWith('$'))
            {
                text = text[1..];
            }

            var splitText = text.Split(' ');
            string last = splitText.Last().ToLower();
            if (last == "max" || last == "5y" || last == "2y" || last == "1y" || last == "ytd" || last == "6m" || last == "3m" || last == "1m" || last == "5d")
            {
                string stock;
                if (splitText.Length > 2)
                {
                    // Historical prices only works with symbols so first search for the symbol
                    try
                    {
                        string searchText = string.Join(" ", splitText[..^1]);
                        BestMatch? stockSearch = await Search(searchText);
                        if (stockSearch == null)
                        {
                            result.Add(new Section($"No stocks found for {searchText}"));
                            return result;
                        }
                        stock = stockSearch.Symbol!;
                    }
                    catch (Exception ex)
                    {
                        result.Add(new Section(ex.Message));
                        return result;
                    }
                }
                else
                {
                    stock = splitText[0];
                }

                string filename;
                try
                {
                    filename = await Historical(stock, last);
                }
                catch (Exception ex)
                {
                    result.Add(new Section(ex.Message));
                    return result;
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    result.Add(new Section($"{filename}"));
                }
                else
                {
                    result.Add(new Image($"https://images.golf1052.com/{filename}", filename));
                }
                return result;
            }

            var initialStockQuote = await iexClient.StockPrices.QuoteAsync(text);
            if (initialStockQuote.Data != null)
            {
                result.Add(new Section(ProcessQuote(initialStockQuote.Data)));
                return result;
            }

            BestMatch? bestMatch;
            try
            {
                bestMatch = await Search(text);
            }
            catch (Exception ex)
            {
                result.Add(new Section(ex.Message));
                return result;
            }

            if (bestMatch == null)
            {
                result.Add(new Section($"No stocks found for {text}"));
                return result;
            }

            var stockQuote = await iexClient.StockPrices.QuoteAsync(bestMatch.Symbol);
            if (!string.IsNullOrWhiteSpace(stockQuote.ErrorMessage))
            {
                result.Add(new Section($"Error: {stockQuote.ErrorMessage}"));
                return result;
            }

            result.Add(new Section(ProcessQuote(stockQuote.Data)));
            return result;
        }

        public async Task<BestMatch?> Search(string searchText)
        {
            SearchResponse searchResponse;
            try
            {
                searchResponse = await SearchAlphaVantage(searchText);
                if (searchResponse.BestMatches.Count == 0)
                {
                    throw new Exception($"No stocks found for {searchText}");
                }
            }
            catch (AlphaVantageApiLimitException)
            {
                throw new Exception("Alpha Vantage search rate limited. Try the stock symbol directly or try again in a minute.");
            }
            catch (AlphaVantageException ex)
            {
                throw new Exception($"Unknown Alpha Vantage error: {ex.Message}");
            }

            return searchResponse.BestMatches.FirstOrDefault(s => s.Currency == "USD");
        }

        private string ProcessQuote(Quote quote)
        {
            string response = $"{quote.symbol}: {quote.companyName}\n" +
                $"*${quote.latestPrice}* (as of {GetDateTimeDisplayString(quote.latestUpdate)})\n" +
                $"{GetChangeString(quote.change)}: {quote.change:C} ({quote.changePercent:P2})\n";

            if (quote.high.HasValue)
            {
                response += $"High: ${quote.high} at {GetDateTimeDisplayString(quote.highTime)}\n";
            }

            if (quote.low.HasValue)
            {
                response += $"Low: ${quote.low} at {GetDateTimeDisplayString(quote.lowTime)}";
            }

            if (!quote.isUSMarketOpen && quote.extendedPrice.HasValue && quote.extendedPriceTime.HasValue &&
                quote.extendedChange.HasValue && quote.extendedChangePercent.HasValue)
            {
                response += $"\n\nExtended Hours\n" +
                    $"*${quote.extendedPrice}* (as of {GetDateTimeDisplayString(quote.extendedPriceTime)})\n" +
                    $"{GetChangeString(quote.extendedChange)}: {quote.extendedChange:C} ({quote.extendedChangePercent:P2})";
            }
            return response;
        }

        private async Task<SearchResponse> SearchAlphaVantage(string text)
        {
            Url url = new Url(BaseUrl)
                .SetQueryParam("function", "SYMBOL_SEARCH")
                .SetQueryParam("keywords", text)
                .SetQueryParam("apikey", Secrets.AlphaVantageApiKey);
            return JsonConvert.DeserializeObject<SearchResponse>(await (await httpClient.GetAsync(url)).Content.ReadAsStringAsync())!;
        }

        private string? GetDateTimeDisplayString(long? epochMilliseconds)
        {
            var info = EpochMillisecondsToDateTimePair(epochMilliseconds);
            if (info == null)
            {
                return null;
            }

            // The display name will be (UTC-05:00) Eastern Time (US & Canada) even when daylight saving time is active
            // This is confusing but this is how Windows displays the name
            return $"{info.Value.Item1.ToString("s")} {info.Value.Item2.DisplayName}";
        }

        private (DateTimeOffset, TimeZoneInfo)? EpochMillisecondsToDateTimePair(long? epochMilliseconds)
        {
            if (epochMilliseconds == null)
            {
                return null;
            }

            TimeZoneInfo timeZoneInfo;
            DateTimeOffset dateTimeOffset;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            }
            else
            {
                timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
            }
            dateTimeOffset = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(epochMilliseconds.Value), timeZoneInfo);
            return (dateTimeOffset, timeZoneInfo);
        }

        private string GetChangeString(decimal? value)
        {
            if (value == null || value == 0)
            {
                return "No change";
            }
            else if (value > 0)
            {
                return "📈";
            }
            else
            {
                return "📉";
            }
        }
    }
}
