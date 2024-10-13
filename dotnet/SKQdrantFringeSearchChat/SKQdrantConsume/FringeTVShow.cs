public class FringeTVShow
{
    // Unique ID for the show/entry
    public ulong Id { get; set; }

    // The URL for the associated content
    public string Url { get; set; }

    // The chunk of text/content extracted from the source (e.g., TV show metadata or description)
    public string Chunk { get; set; }

    // Index or position within the document
    public int Index { get; set; }
    
    // Any additional metadata that you want to associate with each record
    public Dictionary<string, object> Payload { get; set; }

    public FringeTVShow()
    {
        // Initialize the Payload dictionary for custom metadata
        Payload = new Dictionary<string, object>();
    }
}