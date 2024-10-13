using Qdrant.Client;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Protobuf.Collections; // Required for MapField
using Google.Protobuf.WellKnownTypes;
using Qdrant.Client.Grpc; // Required for Value types like StringValue and NumberValue

class Program
{
    static async Task Main(string[] args)
    {
        /*
        var channel = QdrantChannel.ForAddress("https://localhost:6334", new ClientConfiguration
        {
            ApiKey = "<api key>",
            CertificateThumbprint = "<certificate thumbprint>"
        });
        var grpcClient = new QdrantGrpcClient(channel);
        var client = new QdrantClient(grpcClient);
        */

        // Connect to Qdrant without authentication or TLS
        var channel = QdrantChannel.ForAddress("http://localhost:6334");
        var grpcClient = new QdrantGrpcClient(channel);
        var client = new QdrantClient(grpcClient);

        await client.CreateCollectionAsync("fabs_qdrant_alpha", 
            new VectorParams { Size = 100, Distance = Distance.Cosine });

        // generate some vectors
        var random = new Random();
        var points = Enumerable.Range(1, 100).Select(i => new PointStruct
        {
            Id = (ulong)i,
            Vectors = Enumerable.Range(1, 100).Select(_ => (float)random.NextDouble()).ToArray(),
            Payload = 
            { 
                ["color"] = "red", 
                ["rand_number"] = i % 10 
            }
            }).ToList();

        var updateResult = await client.UpsertAsync("fabs_qdrant_alpha", points);

        var queryVector = Enumerable.Range(1, 100).Select(_ => (float)random.NextDouble()).ToArray();

        // return the 5 closest points
        var returnpoints = await client.SearchAsync(
        "fabs_qdrant_alpha",
        queryVector,
        limit: 5);

        Console.WriteLine(returnpoints);

    }
}