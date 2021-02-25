using System;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using botbot.Command.Stock;
using Flurl;
using IEXSharp;
using IEXSharp.Model.Shared.Response;
using Newtonsoft.Json;
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

        public async Task<string> Handle(string text, string userId)
        {
            var initialStockQuote = await iexClient.StockPrices.QuoteAsync(text);
            if (initialStockQuote.Data != null)
            {
                return ProcessQuote(initialStockQuote.Data);
            }

            SearchResponse searchResponse;

            try
            {
                searchResponse = await SearchAlphaVantage(text);
                if (searchResponse.BestMatches.Count == 0)
                {
                    return $"No stocks found for {text}";
                }
            }
            catch (AlphaVantageApiLimitException)
            {
                return "Alpha Vantage search rate limited. Try the stock symbol directly or try again in a minute.";
            }
            catch (AlphaVantageException ex)
            {
                return $"Unknown Alpha Vantage error: {ex.Message}";
            }

            BestMatch stock = searchResponse.BestMatches[0];
            var stockQuote = await iexClient.StockPrices.QuoteAsync(stock.Symbol);
            if (!string.IsNullOrWhiteSpace(stockQuote.ErrorMessage))
            {
                return $"Error: {stockQuote.ErrorMessage}";
            }
            return ProcessQuote(stockQuote.Data);
        }

        private string ProcessQuote(Quote quote)
        {
            string response = $"{quote.symbol}: {quote.companyName}\n" +
                $"{quote.latestPrice} (as of {GetDateTimeDisplayString(quote.latestUpdate)})\n" +
                $"{GetChangeString(quote.change)}: {quote.change:C} ({quote.changePercent:P2})\n";

            if (quote.high.HasValue)
            {
                response += $"High: {quote.high} at {GetDateTimeDisplayString(quote.highTime)}\n";
            }

            if (quote.low.HasValue)
            {
                response += $"Low: {quote.low} at {GetDateTimeDisplayString(quote.lowTime)}";
            }

            if (!quote.isUSMarketOpen && quote.extendedPrice.HasValue && quote.extendedPriceTime.HasValue &&
                quote.extendedChange.HasValue && quote.extendedChangePercent.HasValue)
            {
                response += $"\nExtended Hours\n" +
                    $"{quote.extendedPrice} (as of {GetDateTimeDisplayString(quote.extendedPriceTime)})\n" +
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
            return JsonConvert.DeserializeObject<SearchResponse>(await (await httpClient.GetAsync(url)).Content.ReadAsStringAsync());
        }

        private string? GetDateTimeDisplayString(long? epochMilliseconds)
        {
            var info = EpochMillisecondsToDateTimePair(epochMilliseconds);
            if (info == null)
            {
                return null;
            }

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
            dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(epochMilliseconds.Value).ToOffset(timeZoneInfo.BaseUtcOffset);
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
                return "🔼";
            }
            else
            {
                return "🔽";
            }
        }
    }
}
