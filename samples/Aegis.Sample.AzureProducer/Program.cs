using System;
using System.Threading.Tasks;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Aegis.Producer;
using Aegis.Integrity.Pipelines;
using Aegis.Integrity.Protocol;
using Microsoft.Extensions.Logging;

namespace Aegis.Sample.AzureProducer;

class Program
{
    static async Task Main(string[] args)
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<Program>();

        Console.WriteLine("=================================================");
        Console.WriteLine("   Aegis: Azure Document Intelligence Pipeline   ");
        Console.WriteLine("=================================================");

        if (args.Length < 1)
        {
            Console.WriteLine("\nUsage: dotnet run -- <path-to-pdf>");
            Console.WriteLine("Example: dotnet run -- ../random_input/ikea.pdf");
            return;
        }

        string pdfPath = args[0];
        if (!System.IO.File.Exists(pdfPath))
        {
            Console.WriteLine($"\n[Error] File not found: {pdfPath}");
            return;
        }

        // Note: In a real app, these would come from configuration or environment variables.
        string endpoint = Environment.GetEnvironmentVariable("AZURE_DOCUMENT_INTELLIGENCE_ENDPOINT") ?? "https://YOUR_ENDPOINT.cognitiveservices.azure.com/";
        string key = Environment.GetEnvironmentVariable("AZURE_DOCUMENT_INTELLIGENCE_KEY") ?? "YOUR_KEY";

        if (key == "YOUR_KEY")
        {
            Console.WriteLine("\n[NOTE] No Azure Credentials detected.");
            Console.WriteLine("Please set the following environment variables:");
            Console.WriteLine("1. AZURE_DOCUMENT_INTELLIGENCE_ENDPOINT");
            Console.WriteLine("2. AZURE_DOCUMENT_INTELLIGENCE_KEY");
            return;
        }

        try
        {
            // 1. Initialize the Azure Client
            var client = new DocumentAnalysisClient(new Uri(endpoint), new AzureKeyCredential(key));
            
            Console.WriteLine($"[1] Calling Azure AI Document Intelligence for: {System.IO.Path.GetFileName(pdfPath)}...");
            
            using var stream = System.IO.File.OpenRead(pdfPath);
            var operation = await client.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-layout", stream);
            AnalyzeResult result = operation.Value;

            // 2. Adapt Azure Result to Aegis Manifest
            Console.WriteLine("[2] Mapping Azure results to Aegis Geometric Manifest...");
            var adapter = new DocumentIntelligenceAdapter(loggerFactory.CreateLogger<DocumentIntelligenceAdapter>());
            var manifest = adapter.ToManifest(result);
            
            Console.WriteLine($"    -> Mapped {manifest.Atoms.Count} words and {manifest.Structures.Count} tables.");

            // 3. Pipe to Integrity Pipe for Elastic Chunking
            Console.WriteLine("[3] Generating Verified Chunks (Elastic Chunking)...");
            var pipe = new IntegrityPipe(manifest, loggerFactory.CreateLogger<IntegrityPipe>());
            
            int chunkCount = 0;
            foreach (var chunk in pipe.GenerateChunks(maxTokens: 512))
            {
                chunkCount++;
                Console.WriteLine($"\n--- Chunk {chunkCount} (Page {chunk.Page}) ---");
                Console.WriteLine(chunk.Content.Substring(0, Math.Min(100, chunk.Content.Length)) + "...");
            }
            
            Console.WriteLine($"\n[Success] Processed {chunkCount} chunks with 100% structural integrity.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[Error] {ex.Message}");
            if (ex.InnerException != null) Console.WriteLine($"   Inner: {ex.InnerException.Message}");
        }
    }
}
