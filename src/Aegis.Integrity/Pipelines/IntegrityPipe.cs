
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
    private readonly int _overlapTokens;

    public IntegrityPipe(GeometricManifest manifest, ILogger<IntegrityPipe> logger, int overlapTokens = 0)
    {
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _overlapTokens = Math.Max(0, overlapTokens);
        
        // Ensure mapping is ready
        _manifest.FinalizeMapping();
    }

    /// <summary>
    /// Generates verified chunks stream.
    /// </summary>
    /// <param name="targetTokens">The intended chunk size.</param>
    /// <param name="hardMaxTokens">Forced split limit. Defaults to targetTokens * 1.5.</param>
    /// <returns>A sequence of GeometricChunks representing integrity-preserved chunks.</returns>
    public IEnumerable<GeometricChunk> GenerateChunks(int targetTokens, int? hardMaxTokens = null)
    {
        if (targetTokens <= 0) throw new ArgumentException("Target tokens must be positive.", nameof(targetTokens));

        int maxTokens = hardMaxTokens ?? (int)(targetTokens * 1.2);
        double softBreakThreshold = 0.5;

        int cursor = 0;
        int totalAtoms = _manifest.Atoms.Count;
        int chunkIdx = 0;

        while (cursor < totalAtoms)
        {
            // 1. Proposed cut point based on Target
            int end = FindTokenBoundary(cursor, targetTokens);
            string reason = "TargetReached";
            
            // 2. GIP 2.3 Optimized Structural Collision Check (O(1))
            var collision = _manifest.GetStructuresAt(end).FirstOrDefault();

            if (collision != null)
            {
                // GIP 2.0: Soft-Break & Balanced Integrity Logic
                int structureSize = collision.End - collision.Start;
                double proximityToEnd = (double)(end - collision.Start) / structureSize;

                if (structureSize > maxTokens || proximityToEnd > softBreakThreshold)
                {
                    if (structureSize > maxTokens)
                    {
                        _logger.LogInformation("Soft-Break triggered for oversized structure {Type} at atom {End}", collision.Type, end);
                        reason = $"SoftBreak-{collision.Type}";
                    }
                    else
                    {
                        _logger.BackpressureApplied(end, collision.Type, "Advance");
                        end = collision.End + 1;
                        reason = $"Preserved-{collision.Type}";
                    }
                }
                else
                {
                    // Recede: back off to before the table starts
                    _logger.BackpressureApplied(end, collision.Type, "Recede");
                    end = collision.Start;
                    reason = "Backpressure-Recede";
                    
                    // Prevent infinite loop
                    if (end <= cursor)
                    {
                        _logger.LogWarning("Oversized structure at {Cursor}, being forced to split at Target.", cursor);
                        end = FindTokenBoundary(cursor, targetTokens);
                        reason = $"ForcedSplit-{collision.Type}";
                    }
                }
            }

            // Safe-guard end index
            end = Math.Min(end, totalAtoms);

            // GIP 2.1 Density Fix: Merging trailing fragments
            int densityThreshold = Math.Max(2, (int)(targetTokens * 0.2));
            int remaining = totalAtoms - end;
            if (remaining > 0 && remaining < densityThreshold)
            {
                end = totalAtoms;
            }
            
            // 4. Emit Chunk
            var chunkAtoms = _manifest.Atoms.GetRange(cursor, end - cursor);
            if (chunkAtoms.Count == 0) break;

            // Semantic Anchoring (Optimized Ancestry)
            var markers = new List<string> { $"[Page {chunkAtoms.First().Page}]" };
            
            // Sample start and end for structural tagging (O(1))
            var startStructures = _manifest.GetStructuresAt(cursor);
            var endStructures = _manifest.GetStructuresAt(end - 1);
            
            var structuralTypes = startStructures.Concat(endStructures)
                .Select(s => s.Type)
                .Distinct();

            foreach (var type in structuralTypes)
            {
                markers.Add($"[{type}]");
            }

            string markerPrefix = string.Join(" ", markers) + " ";
            string content = markerPrefix + string.Join(" ", chunkAtoms.Select(a => a.Text));
            
            int tokenCount = chunkAtoms.Sum(a => a.TokenCount);
            int startIdx = chunkAtoms.First().Index;
            int endIdx = chunkAtoms.Last().Index;
            int page = chunkAtoms.First().Page;

            _logger.ChunkGenerated(chunkIdx++, tokenCount, reason);
            yield return new GeometricChunk(content, startIdx, endIdx, page, tokenCount, reason);

            // GIP 2.0: Geometric Overlap
            if (_overlapTokens > 0)
            {
                int newCursor = FindOverlapIndex(end, _overlapTokens);
                cursor = Math.Max(cursor + 1, newCursor); // Ensure at least 1 atom progress
            }
            else
            {
                cursor = end;
            }
        }
    }

    private int FindOverlapIndex(int end, int overlapLimit)
    {
        int currentTokens = 0;
        int i = end - 1;
        while (i >= 0)
        {
            currentTokens += _manifest.Atoms[i].TokenCount;
            if (currentTokens > overlapLimit) return i + 1;
            i--;
        }
        return 0;
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
        
        if (i == start && start < _manifest.Atoms.Count) return start + 1;
        
        return i;
    }
}
