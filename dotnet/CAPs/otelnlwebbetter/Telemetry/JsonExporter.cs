using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using OpenTelemetry;
using OpenTelemetry.Trace;
using System;
using System.Text.RegularExpressions;


namespace OtelNLWebBetter.Telemetry
{
    public class JsonExporter : BaseExporter<Activity>
    {
        private readonly string _outputPath;
        private readonly object _fileLock = new();

        public JsonExporter(string outputPath = "trace-export.jsonl")
        {
            _outputPath = outputPath;

            // Clear the file on start
            File.WriteAllText(_outputPath, string.Empty);
        }

        public override ExportResult Export(in Batch<Activity> batch)
        {
            foreach (var activity in batch)
            {
                var evt = new SchemaEvent
                {
                    Type = "Event",
                    Name = activity.DisplayName,
                    StartDate = activity.StartTimeUtc.ToString("o"),
                    EndDate = activity.Duration != TimeSpan.Zero
                        ? activity.StartTimeUtc.Add(activity.Duration).ToString("o")
                        : null,
                    Location = activity.Source.Name,
                    Description = GetSummaryFromTags(activity),
                    Identifier = activity.TraceId.ToString(),
                    Text = $"{activity.DisplayName}: {GetSummaryFromTags(activity)}"
                };

                var json = JsonSerializer.Serialize(evt);
                lock (_fileLock)
                {
                    File.AppendAllText(_outputPath, json + Environment.NewLine);
                }
            }

            return ExportResult.Success;
        }

        private static string GetSummaryFromTags(Activity activity)
        {
            var sb = new StringBuilder();

            foreach (var tag in activity.TagObjects)
            {
                sb.Append($"{tag.Key}: {tag.Value}; ");
            }

            return sb.ToString().Trim();
        }

        /*
        private class SchemaEvent
        {
            [JsonPropertyName("@type")]
            public string Type { get; set; } = "Event";

            public string Name { get; set; } = "";
            public string? StartDate { get; set; }
            public string? EndDate { get; set; }
            public string? Location { get; set; }
            public string? Description { get; set; }
            public string? Identifier { get; set; }

            [JsonPropertyName("text")]
            public string? Text { get; set; }
        }
        */

        private class SchemaEvent
        {
            [JsonPropertyName("@type")]
            public string Type { get; set; } = "Event";

            public string Name { get; set; } = "";
            public string? StartDate { get; set; }
            public string? EndDate { get; set; }
            public string? Location { get; set; }
            public string? Description { get; set; }
            public string? Identifier { get; set; }

            [JsonPropertyName("text")]
            public string? Text { get; set; }

            // Derived field: synthetic URL (required by NLWeb)
            [JsonPropertyName("url")]
            public string Url => $"otel://{Name}/{Identifier}";

            // Derived field: user-friendly title for display and embedding
            [JsonPropertyName("name")]
            public string Title => $"{Name} - {ExtractSummary()}";

            private string ExtractSummary()
            {
                // Try to get a plugin name or fallback to first part of text
                if (!string.IsNullOrEmpty(Description))
                {
                    var match = Regex.Match(Description, @"plugin\.name:\s*(\w+)");
                    if (match.Success)
                        return match.Groups[1].Value;
                }

                if (!string.IsNullOrEmpty(Text))
                {
                    return Text.Length > 40 ? Text.Substring(0, 40) + "..." : Text;
                }

                return Name;
            }
        }

        
    }
}
