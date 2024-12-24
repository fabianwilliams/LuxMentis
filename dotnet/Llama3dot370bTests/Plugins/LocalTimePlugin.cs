using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace LLAMA3DOT370BTESTS.Plugins
{
    public sealed class LocalTimePlugin
    {
        [KernelFunction, Description("Retrieves the current date in YYYY-MM-DD format.")]
        public static string GetCurrentLocalDate()
        {
            return DateTime.Now.ToString("yyyy-MM-dd");
        }

        [KernelFunction, Description("Gets the future date by adding a number of days to the current date.")]
        public static string GetFutureDate(int days)
        {
            return DateTime.Now.AddDays(days).ToString("yyyy-MM-dd");
        }
    }
}