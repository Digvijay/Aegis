
using System;
using System.Collections.Generic;
using System.Linq;
using Aegis.Integrity.Protocol;
using Microsoft.Extensions.Logging;
using Aegis.Integrity.Diagnostics;

namespace Aegis.Integrity.Pipelines;

/// <summary>
/// A pipeline component that consumes atoms and produces chunks that respect geometric integrity.
/// Implements "Elastic Chunking" and "Backpressure".
/// </summary>
public class IntegrityPipe
{
    private readonly GeometricManifest _manifest;
    private readonly ILogger<IntegrityPipe> _logger;

    public IntegrityPipe(GeometricManifest manifest, ILogger<IntegrityPipe> logger)
    {
        _manifest = manifest;
        _logger = logger;
    }

    /// <summary>
    /// Generates verified chunks stream.
    /// </summary>
    /// <param name="maxTokens">The target token limit per chunk.</param>
    /// <returns>A sequence of GeometricChunks representing integrity-preserved chunks.</returns>
    public IEnumerable<GeometricChunk> GenerateChunks(int maxTokens)
    {
        int cursor = 0;
        int totalAtoms = _manifest.Atoms.Count;
        int chunkIdx = 0;

        while (cursor < totalAtoms)
        {
            // 1. Proposed cut point based on Token Limit
            int end = FindTokenBoundary(cursor, maxTokens);
            string reason = "TokenLimit";
            
            // 2. Check for Structural Collision
            // Is 'end' inside a No-Cut Zone?
            // "Inside" means: Start < end < End
            var collision = _manifest.Structures.FirstOrDefault(s => end > s.Start && end < s.End);

            if (collision != null)
            {
                // 3. Negotiate Boundary (Backpressure)
                // The "Pivot Point" Strategy:
                // We calculate the midpoint of the detected structure (Table/List).
                // If the proposed cut falls AFTER the midpoint, we "Advance" to include the whole structure.
                // If the proposed cut falls BEFORE the midpoint, we "Recede" to exclude the structure entirely.
                // This ensures we never chop a table in half.
                
                int structureMidpoint = collision.Start + ((collision.End - collision.Start) / 2);

                if (end > structureMidpoint)
                {
                    // Advance: The boundary is past the midpoint, so we include the entire structure.
                    // This ensures the table or list remains contiguous.
                    _logger.BackpressureApplied(end, collision.Type, "Advance");
                    end = collision.End + 1;
                    reason = $"Preserved-{collision.Type}";
                }
                else
                {
                    // Recede: We are just entering, back off to before the table starts
                    _logger.BackpressureApplied(end, collision.Type, "Recede");
                    end = collision.Start;
                    reason = "Backpressure-Recede";
                    
                    // Edge Case: If the table starts exactly at cursor, we MUST consume it or we loop forever.
                    // This happens if a single structure > maxTokens. 
                    if (end <= cursor)
                    {
                        end = collision.End + 1;
                        reason = $"Oversize-{collision.Type}";
                    }
                }
            }

            // Safe-guard end index
            end = Math.Min(end, totalAtoms);
            
            // 4. Emit Chunk
            var chunkAtoms = _manifest.Atoms.GetRange(cursor, end - cursor);
            int tokenCount = chunkAtoms.Sum(a => a.TokenCount);
            string content = string.Join(" ", chunkAtoms.Select(a => a.Text));
            
            // Metadata
            int startIdx = chunkAtoms.First().Index;
            int endIdx = chunkAtoms.Last().Index;
            int page = chunkAtoms.First().Page; // Attribution to start page

            _logger.ChunkGenerated(chunkIdx++, tokenCount, reason);
            yield return new GeometricChunk(content, startIdx, endIdx, page, tokenCount, reason);

            // 5. Advance Cursor
            cursor = end;
        }
    }

    private int FindTokenBoundary(int start, int limit)
    {
        int currentTokens = 0;
        int i = start;
        while (i < _manifest.Atoms.Count)
        {
            currentTokens += _manifest.Atoms[i].TokenCount;
            if (currentTokens > limit) break;
            i++;
        }
        
        // Ensure we consume at least one atom if possible
        if (i == start && start < _manifest.Atoms.Count) return start + 1;
        
        return i;
    }
}
