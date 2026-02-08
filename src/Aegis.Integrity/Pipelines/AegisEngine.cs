
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UglyToad.PdfPig;
using Aegis.Integrity.Protocol;
using Aegis.Integrity.Discovery;
using Microsoft.Extensions.Logging;

namespace Aegis.Integrity.Pipelines;

/// <summary>
/// The main entry point for the Aegis system.
/// Orchestrates the flow from PDF Stream -> Geometric Atoms -> Grid Law Discovery -> Integrity Pipe -> Verified Chunks.
/// Uses IAsyncEnumerable for high-performance, low-memory streaming.
/// </summary>
public class AegisEngine
{
    private readonly GridLawDetector _detector;
    private readonly ILoggerFactory _loggerFactory;

    public AegisEngine(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _detector = new GridLawDetector(_loggerFactory.CreateLogger<GridLawDetector>());
    }

    /// <summary>
    /// Streams verified chunks from a PDF stream.
    /// </summary>
    /// <param name="pdfStream">The input PDF stream.</param>
    /// <param name="maxTokens">The maximum token count per chunk.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async stream of verified chunks.</returns>
    public async IAsyncEnumerable<AegisChunk> StreamVerifiedChunksAsync(
        Stream pdfStream,
        int maxTokens,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // UglyToad.PdfPig uses a synchronous API for opening, but we can wrap the *processing* in async iterator
        // Ideally we'd validte the stream first.
        
        // Note: PdfDocument.Open reads the whole stream if it's seekable, or buffers if not.
        // For true streaming we rely on PdfPig's implementation details, but passing a stream is the standard way.
        using var document = PdfDocument.Open(pdfStream);

        // We process page by page to keep memory footprint low(er) than loading whole text
        // But for cross-page tables, we might need a sliding window. 
        // For V1 "L64" implementation, we treat Page as a hard boundary for simplicity unless we implemented multi-page binding.
        // The implementation plan implies streaming atoms. 
        
        foreach (var page in document.GetPages())
        {
            if (cancellationToken.IsCancellationRequested) yield break;

            // 1. FAST DISCOVERY: Map the atoms for this page
            var atoms = new List<GeometricAtom>();
            int atomIndex = 0;
            
            foreach(var word in page.GetWords())
            {
                // Simple token estimation: 1 word ~ 1.3 tokens? Or char count?
                // Whitepaper said: "EstimateTokens(string text) => (int)Math.Ceiling(text.Length / 4.0);"
                int tokenCount = (int)Math.Ceiling(word.Text.Length / 4.0);
                if (tokenCount < 1) tokenCount = 1;

                atoms.Add(new GeometricAtom(
                    word.Text,
                    new BoundingBox(word.BoundingBox.Left, word.BoundingBox.Bottom, word.BoundingBox.Width, word.BoundingBox.Height),
                    page.Number,
                    tokenCount
                ) { Index = atomIndex++ });
            }

            // 2. Discover Structures (Grid Law)
            var noCutZones = _detector.DetectTableZones(atoms);

            // 3. Create Manifest
            var manifest = new GeometricManifest
            {
                Atoms = atoms,
                Structures = noCutZones
            };

            // 4. Pipe Execution
            var pipe = new IntegrityPipe(manifest, _loggerFactory.CreateLogger<IntegrityPipe>());
            var chunks = pipe.GenerateChunks(maxTokens);

            foreach (var chunk in chunks)
            {
                 // Map internal GeometricChunk to public AegisChunk
                 yield return new AegisChunk(chunk.Content, chunk.Page, chunk.TokenCount, chunk.Discriminator);
            }

            // Yield control to allow async processing
            await Task.Yield();
        }
    }
}

/// <summary>
/// Represents a verified chunk produced by the Aegis Engine.
/// </summary>
/// <param name="Content">The text content of the chunk.</param>
/// <param name="Page">The page number it originated from.</param>
/// <param name="TokenCount">The number of tokens in the chunk.</param>
/// <param name="Discriminator">The reason for the chunk boundary.</param>
public record AegisChunk(string Content, int Page, int TokenCount, string Discriminator);
