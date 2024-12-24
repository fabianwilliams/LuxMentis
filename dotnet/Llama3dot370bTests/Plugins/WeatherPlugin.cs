using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace LLAMA3DOT370BTESTS.Plugins
{
    public sealed class WeatherPlugin(string apiKey)
    {
        HttpClient client = new HttpClient();

        [KernelFunction, Description("Gets the weather details of a given location")]
        [return: Description("Weather details")]
        public async Task<string> GetWeatherAsync([Description("name of the location")] string locationName)
        {
            string url = $"http://api.weatherapi.com/v1/current.json?key={apiKey}&q={locationName}&aqi=no";

            HttpResponseMessage response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();

            return responseBody;
        }
    }
}