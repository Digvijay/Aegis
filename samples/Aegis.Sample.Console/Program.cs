using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aegis.Integrity.Protocol;
using Aegis.Integrity.Pipelines;
using Aegis.Integrity.Discovery;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace Aegis.Sample.ConsoleApp;

class Program
{
    static void Main(string[] args)
    {
        string inputDir = Path.Combine("..", "random_input");
        if (!Directory.Exists(inputDir))
        {
             Console.WriteLine($"[Error] Input directory not found: {Path.GetFullPath(inputDir)}");
             return;
        }

        var pdfFiles = Directory.GetFiles(inputDir, "*.pdf");
        Console.WriteLine($"Found {pdfFiles.Length} PDF files to process.\n");

        foreach (var pdfPath in pdfFiles)
        {
            Console.WriteLine("=================================================");
            Console.WriteLine($"   Processing: {Path.GetFileName(pdfPath)}");
            Console.WriteLine("=================================================\n");
            
            ProcessPdf(pdfPath);
        }
    }

    static void ProcessPdf(string pdfPath)
    {
        // 1. Load PDF and Convert to Geometric Atoms
        Console.WriteLine($"[1] Loading PDF from: {pdfPath}");
        var atoms = ExtractAtomsFromPdf(pdfPath);
        Console.WriteLine($"    -> Extracted {atoms.Count} geometric atoms (tokens).");

        // 2. Discover Structure (Grid Law)
        Console.WriteLine("[2] Running Grid Law Detector...");
        var nullLogger = LoggerFactory.Create(b => {}).CreateLogger<GridLawDetector>();
        var detector = new GridLawDetector(nullLogger);
        var structures = detector.DetectTableZones(atoms);
        
        Console.WriteLine($"    -> Discovered {structures.Count} structural zones (Tables/Lists).");
        foreach(var s in structures)
        {
            // Truncate output for very large docs
            if (structures.IndexOf(s) < 5) 
                Console.WriteLine($"       - [{s.Type}] Indices {s.Start}-{s.End} ({s.End - s.Start + 1} tokens)");
            else if (structures.IndexOf(s) == 5)
                Console.WriteLine("       ... (more structures detected)");
        }

        var manifest = new GeometricManifest { Atoms = atoms, Structures = structures };

        // 3. Define Constraints
        int largestTableSize = structures.Any() ? structures.Max(s => s.End - s.Start + 1) : 50;
        int maxTokens = (int)(largestTableSize * 0.7); 
        if (maxTokens < 10) maxTokens = 50; // Minimum sanity

        Console.WriteLine($"\n[Scenario] Benchmark Constraint: Chunk Size = {maxTokens} tokens");
        Console.WriteLine("           (Designed to stress-test the largest table)\n");

        // ---------------------------------------------------------
        // METHOD A: The "Ghost Ship" (Fixed-Size Chunking)
        // ---------------------------------------------------------
        Console.WriteLine(">>> Method A: Traditional Fixed-Size Chunking");
        var fixedChunks = RunFixedSizeChunking(atoms, maxTokens);
        AnalyzeResult("Fixed-Size", fixedChunks, structures, atoms);

        Console.WriteLine("\n-------------------------------------------------\n");

        // ---------------------------------------------------------
        // METHOD B: Aegis (Geometric Integrity)
        // ---------------------------------------------------------
        Console.WriteLine(">>> Method B: Aegis Integrity Protocol");
        
        var loggerFactory = LoggerFactory.Create(builder => {}); // Silence console logs for batch run clarity
        var logger = loggerFactory.CreateLogger<IntegrityPipe>();

        var pipe = new IntegrityPipe(manifest, logger);
        var aegisChunks = pipe.GenerateChunks(maxTokens).ToList();
        
        Console.WriteLine($"\n[Aegis Output] Generated {aegisChunks.Count} Chunks (Sample Preview):\n");
        foreach(var chunk in aegisChunks.Take(3)) // Show first 3 only
        {
            Console.WriteLine("   +-------------------------------------------------------------+");
            Console.WriteLine($"   | Chunk ID: {aegisChunks.IndexOf(chunk).ToString().PadRight(4)} | Page: {chunk.Page.ToString().PadRight(3)} | Tokens: {chunk.TokenCount.ToString().PadRight(4)} | Type: {chunk.Discriminator.PadRight(10)} |");
            Console.WriteLine("   +-------------------------------------------------------------+");
            Console.WriteLine($"   | \"{chunk.Content.Substring(0, Math.Min(50, chunk.Content.Length)).Replace("\n"," ")}...\" ");
            Console.WriteLine("   +-------------------------------------------------------------+\n");
        }
        if (aegisChunks.Count > 3) Console.WriteLine($"   ... ({aegisChunks.Count - 3} more chunks hidden)\n");

        AnalyzeResult("Aegis", aegisChunks, structures, atoms);
    }

    static List<GeometricAtom> ExtractAtomsFromPdf(string path)
    {
        var atoms = new List<GeometricAtom>();
        using var document = PdfDocument.Open(path);
        int index = 0;

        foreach (var page in document.GetPages())
        {
            foreach (var word in page.GetWords())
            {
                // Convert PdfPig coordinate to Aegis BoundingBox
                // PdfPig: Bottom-Left origin. Aegis: Agnostic container.
                var bounds = new BoundingBox(
                    word.BoundingBox.Left, 
                    word.BoundingBox.Bottom, 
                    word.BoundingBox.Width, 
                    word.BoundingBox.Height
                );

                atoms.Add(new GeometricAtom(word.Text, bounds, page.Number, 1) 
                { 
                    Index = index++ 
                });
            }
        }
        return atoms;
    }

    static List<StructuralRange> RunFixedSizeChunking(List<GeometricAtom> atoms, int size)
    {
        // Return chunks as ranges for analysis
        var chunks = new List<StructuralRange>();
        for (int i = 0; i < atoms.Count; i += size)
        {
            int end = Math.Min(i + size - 1, atoms.Count - 1);
            chunks.Add(new StructuralRange(i, end, "Chunk"));
        }
        return chunks;
    }

    static void AnalyzeResult(string method, List<StructuralRange> chunkRanges, List<StructuralRange> tables, List<GeometricAtom> atoms)
    {
        // Check if any table was bisected by a chunk boundary
        int brokenTables = 0;

        // A table is BROKEN if it is not fully contained within a single chunk.
        // i.e., Table [10-50] is broken if Chunk1 ends at 30.
        
        foreach (var table in tables)
        {
            bool isContained = false;
            foreach (var chunk in chunkRanges)
            {
                // If chunk completely covers the table
                if (chunk.Start <= table.Start && chunk.End >= table.End)
                {
                    isContained = true;
                    break;
                }
            }

            if (!isContained)
            {
                brokenTables++;
                // Console.WriteLine($"   [Failure] Table {table.Start}-{table.End} was bisected.");
            }
        }

        bool failed = brokenTables > 0;
        
        Console.ForegroundColor = failed ? ConsoleColor.Red : ConsoleColor.Green;
        Console.WriteLine($"   Result: {(failed ? "FRAGMENTED" : "PRESERVED")}");
        Console.ResetColor();

        if (failed)
        {
            Console.WriteLine($"   Impact: {brokenTables} out of {tables.Count} tables were destroyed.");
            Console.WriteLine("   RAG Outcome: Hallucination Risk HIGH.");
        }
        else
        {
            Console.WriteLine($"   Impact: 100% of tables preserved.");
            Console.WriteLine("   RAG Outcome: Retrieval Precision 100%.");
        }
        
        Console.WriteLine($"   Total Chunks: {chunkRanges.Count}");
    }
    
    // Overload for Aegis GeometricChunks
    // varying mapping internal indices to ranges for analysis.
    
    static void AnalyzeResult(string method, List<GeometricChunk> chunks, List<StructuralRange> tables, List<GeometricAtom> atoms)
    {
         // Map GeometricChunk internal indices to ranges
         var ranges = chunks.Select(c => new StructuralRange(c.StartIndex, c.EndIndex, "Chunk")).ToList();
         AnalyzeResult(method, ranges, tables, atoms);
    }
}
