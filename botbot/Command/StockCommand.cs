using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ThreeFourteen.AlphaVantage;
using Flurl;
using Newtonsoft.Json;
using botbot.Command.Stock;
using ThreeFourteen.AlphaVantage.Model;

namespace botbot.Command
{
    public class StockCommand : ISlackCommand
    {
        private const string BaseUrl = "https://www.alphavantage.co/query";

        private AlphaVantage client;
        private HttpClient httpClient;

        public StockCommand()
        {
            client = new AlphaVantage(Secrets.AlphaVantageApiKey);
            httpClient = new HttpClient();
        }

        public async Task<string> Handle(string text, string userId)
        {
            Url url = new Url(BaseUrl)
                .SetQueryParam("function", "SYMBOL_SEARCH")
                .SetQueryParam("keywords", text)
                .SetQueryParam("apikey", Secrets.AlphaVantageApiKey);
            SearchResponse searchResponse = JsonConvert.DeserializeObject<SearchResponse>(await (await httpClient.GetAsync(url)).Content.ReadAsStringAsync());
            if (searchResponse.BestMatches.Count == 0)
            {
                return $"No stocks found for {text}";
            }

            BestMatch stock = searchResponse.BestMatches[0];
            try
            {
                // the Intraday result doesn't contain the open/close price for a day, just the open and close price for a interval
                var dailyResult = await client.Stocks.Daily(stock.Symbol).GetAsync();
                var intraDayResult = await client.Stocks.IntraDay(stock.Symbol)
                    .SetInterval(Interval.OneMinute)
                    .GetAsync();
                TimeSeriesEntry firstDailyResult = dailyResult.Data.FirstOrDefault();
                TimeSeriesEntry firstIntradayResult = intraDayResult.Data.FirstOrDefault();
                if (firstDailyResult == null || firstIntradayResult == null)
                {
                    return $"No stock info found for {stock.Symbol}: {stock.Name}";
                }

                double change = firstIntradayResult.Close - firstDailyResult.Open;
                double percentChange = PercentChange(firstIntradayResult.Close, firstDailyResult.Open);
                string changeString;
                if (change > 0)
                {
                    changeString = "Up";
                }
                else if (change == 0)
                {
                    changeString = "None";
                }
                else
                {
                    changeString = "Down";
                }
                return $"{stock.Symbol}: {stock.Name}\n{firstIntradayResult.Close}\n{changeString}: {change:C} ({percentChange:P2})";
            }
            catch (AlphaVantageApiLimitException)
            {
                return "Rate limited. Try again in a minute.";
            }
            catch (AlphaVantageException ex)
            {
                return $"Unknown error\n{ex.Message}";
            }
            catch (Exception ex)
            {
                return $"Some other error: {ex.Message}";
            }
        }

        private double PercentChange(double newPrice, double oldPrice)
        {
            return (newPrice - oldPrice) / oldPrice;
        }
    }
}
