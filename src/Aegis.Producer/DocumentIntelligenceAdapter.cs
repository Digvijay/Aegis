
using System;
using System.Collections.Generic;
using System.Linq;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Aegis.Integrity.Protocol;

using Microsoft.Extensions.Logging;
using Aegis.Integrity.Diagnostics;

namespace Aegis.Producer;

/// <summary>
/// Adapts Azure AI Document Intelligence results into the Aegis Geometric Manifest.
/// </summary>
public class DocumentIntelligenceAdapter
{
    private readonly ILogger<DocumentIntelligenceAdapter> _logger;

    public DocumentIntelligenceAdapter(ILogger<DocumentIntelligenceAdapter> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Converts an AnalyzeResult to a GeometricManifest.
    /// maps Azure's "Words" to "GeometricAtoms" and "Tables" to "StructuralRanges".
    /// </summary>
    /// <param name="result">The result from Azure AI Document Intelligence.</param>
    /// <returns>A populated GeometricManifest.</returns>
    public GeometricManifest ToManifest(AnalyzeResult result)
    {
        var manifest = new GeometricManifest();
        int atomIndex = 0;

        foreach (var page in result.Pages)
        {
            // 1. Map Words to Atoms
            foreach (var word in page.Words)
            {
                // Azure Document Intelligence provides BoundingPolygon [TL, TR, BR, BL].
                // We map this to a standard BoundingBox (X, Y, Width, Height).
                var x = word.BoundingPolygon[0].X;
                var y = word.BoundingPolygon[0].Y;
                var width = word.BoundingPolygon[2].X - word.BoundingPolygon[0].X;
                var height = word.BoundingPolygon[2].Y - word.BoundingPolygon[0].Y;

                manifest.Atoms.Add(new GeometricAtom(
                    word.Content,
                    new BoundingBox(x, y, width, height),
                    page.PageNumber,
                    EstimateTokenCount(word.Content)
                ) { Index = atomIndex++ });
            }
        }

        // 2. Map Tables to StructuralRanges
        // specific Strategy:
        // Azure Document Intelligence defines tables using "Spans" (character offsets in the raw content).
        // Aegis defines structures using "Atom Indices" (sequence order of tokens).
        // To bridge this, we flatten all Words into a list of (AtomIndex, Offset, Length) tuples.
        // Then, for each table, we find the range of AtomIndices that overlapping with the table's character spans.
        
        // Flatten all words with their spans and assigned AtomIndex.
        var wordSpans = new List<(int AtomIndex, int Offset, int Length)>();
        int idx = 0;
        foreach(var page in result.Pages)
        {
            foreach(var word in page.Words)
            {
                wordSpans.Add((idx++, word.Span.Index, word.Span.Length));
            }
        }

        foreach (var table in result.Tables)
        {
            // A table has multiple cells, covering a range of spans.
            // We want the *logical range* of atoms that this table covers.
            // Assuming the table is contiguous in the reading order (which Azure usually ensures for stream).
            
            var tableSpans = table.Cells.SelectMany(c => c.Spans).ToList();
            if(!tableSpans.Any()) continue;
            
            int min = tableSpans.Min(s => s.Index);
            int max = tableSpans.Max(s => s.Index + s.Length);

            // Find atoms within [min, max)
            // Using the wordSpans list
            var included = wordSpans.Where(w => w.Offset >= min && (w.Offset + w.Length) <= max).ToList();
            
            if (included.Count > 0)
            {
                int startAtom = included.Min(w => w.AtomIndex);
                int endAtom = included.Max(w => w.AtomIndex); // Inclusive
                
                // Add structure
                manifest.Structures.Add(new StructuralRange(startAtom, endAtom, "Table"));
            }
        }

        _logger.MappingComplete(result.Pages.Count, manifest.Atoms.Count, manifest.Structures.Count);
        return manifest;
    }

    private int EstimateTokenCount(string text)
    {
        return (int)Math.Ceiling(text.Length / 4.0);
    }
}
