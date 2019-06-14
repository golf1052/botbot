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
    public class StockCommand
    {
        private const string BaseUrl = "https://www.alphavantage.co/query";

        private AlphaVantage client;
        private HttpClient httpClient;

        public StockCommand()
        {
            client = new AlphaVantage(Secrets.AlphaVantageApiKey);
            httpClient = new HttpClient();
        }

        public async Task<string> Handle(string text)
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
                var result = await client.Stocks.Daily(stock.Symbol).GetAsync();
                TimeSeriesEntry firstEntry = result.Data.FirstOrDefault();
                if (firstEntry == null)
                {
                    return $"No stock info found for {stock.Symbol}: {stock.Name}";
                }

                double change = firstEntry.Close - firstEntry.Open;
                double percentChange = PercentChange(firstEntry.Close, firstEntry.Open);
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
                return $"{stock.Symbol}: {stock.Name}\n{firstEntry.Close}\n{changeString}: {change:C} ({percentChange:P2})";
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
