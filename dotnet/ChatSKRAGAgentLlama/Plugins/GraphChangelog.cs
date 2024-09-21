using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Services;
using SimpleFeedReader;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;

public class GraphChangelog
{
    public const string GCLRSS = "https://developer.microsoft.com/en-us/graph/changelog/rss";

    // Method to fetch and parse the RSS feed manually using HttpClient
    public async Task<List<RSSItem>> FetchRssItemsAsync()
    {
        using (HttpClient client = new HttpClient())
        {
            // Fetch the RSS feed content
            var response = await client.GetStringAsync(GCLRSS);
            var xdoc = XDocument.Parse(response);

            // Parse the RSS feed and extract relevant items
            var items = (from item in xdoc.Descendants("item")
                         select new RSSItem
                         {
                             Title = item.Element("title")?.Value,
                             Link = item.Element("link")?.Value,  // Extract the link element
                             Description = WebUtility.HtmlDecode(item.Element("description")?.Value),
                             PublishDate = DateTime.TryParse(item.Element("pubDate")?.Value, out var date) ? date : DateTime.MinValue
                         }).OrderByDescending(i => i.PublishDate)  // Sort by publish date (most recent first)
                           .Take(10)  // Take the top 10 items
                           .ToList();

            return items;
        }
    }

    // Helper method to format the output for the LLM
    public string FormatForLLM(List<RSSItem> items)
    {
        var formattedOutput = "";

        foreach (var item in items)
        {
            // Format each RSS item with title, link, description, and publish date
            formattedOutput += $"**Title:** {item.Title}\n";
            formattedOutput += $"**Link:** {item.Link}\n";  // Include the link in the output
            formattedOutput += $"**Description:** {item.Description}\n";
            formattedOutput += $"**Publish Date:** {item.PublishDate}\n\n";
        }

        return formattedOutput;
    }

    // Updated Plugin Method to fetch the top 10 most recent items from the Microsoft Graph RSS feed
    [KernelFunction("get_formatted_graphlog_feed")]
    [Description("Fetches the top 10 most recent items from the Microsoft Graph RSS feed and returns a formatted string")]
    [return: Description("The formatted feed for Microsoft Graph Changelog items as a string")]
    public async Task<string> GetFormattedChangeLogFeedAsync()
    {
        // Fetch and parse the RSS feed manually
        var recentItems = await FetchRssItemsAsync();

        // Format the items for the LLM
        var formattedFeed = FormatForLLM(recentItems);

        // Return the formatted feed string
        return formattedFeed;
    }
}

// Helper class to represent an RSS item
public class RSSItem
{
    public string Title { get; set; }
    public string Link { get; set; }  // Store the link here
    public string Description { get; set; }
    public DateTime PublishDate { get; set; }
}