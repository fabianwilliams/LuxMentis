using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace LLAMA3DOT370BTESTS.Plugins
{
    public sealed class LocalTimePlugin
    {
        [KernelFunction, Description("Retrieves the current time in EST.")]
        public static string GetCurrentLocalDate()
        {
            var estZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            var estTime = TimeZoneInfo.ConvertTime(DateTime.Now, estZone);
            return $"{estTime:yyyy-MM-dd HH:mm:ss} (EST)";
        }
    }
}