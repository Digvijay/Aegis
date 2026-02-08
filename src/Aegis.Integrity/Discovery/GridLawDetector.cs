
using System;
using System.Collections.Generic;
using System.Linq;
using Aegis.Integrity.Protocol;
using Microsoft.Extensions.Logging;
using Aegis.Integrity.Diagnostics;

namespace Aegis.Integrity.Discovery;

/// <summary>
/// Detects tabular structures by analyzing spatial frequency and alignment of atoms.
/// No OCR required. Pure coordinate math.
/// </summary>
public class GridLawDetector
{
    // 5.0 Points is a heuristic derived from standard PDF line heights.
    // It allows for minor misalignments (e.g., scanning noise or kerning) while strictly enforcing grid structure.
    private const double AlignmentThreshold = 5.0; // Points (variance allowed)
    private readonly ILogger<GridLawDetector> _logger;

    public GridLawDetector(ILogger<GridLawDetector> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Scans a list of geometric atoms and returns ranges identified as tables/grids.
    /// </summary>
    /// <param name="atoms">List of atoms on a page.</param>
    /// <returns>List of structural ranges representing no-cut zones.</returns>
    public List<StructuralRange> DetectTableZones(List<GeometricAtom> atoms)
    {
        if (atoms.Count > 0)
        {
            _logger.DiscoveryStarted(atoms[0].Page, atoms.Count);
        }

        var zones = new List<StructuralRange>();
        
        // Group atoms into horizontal lines (clustered by Y coordinate)
        var rows = atoms.GroupBy(a => Math.Round(a.Bounds.Y, 1))
                        .OrderByDescending(g => g.Key) // Top to bottom
                        .ToList();

        int? startRowIndex = null;

        for (int i = 1; i < rows.Count; i++)
        {
            // Compare logical "columns" (X-starts) of current row vs previous row
            bool isAligned = CheckVerticalAlignment(rows[i - 1], rows[i]);

            if (isAligned)
            {
                if (startRowIndex == null) startRowIndex = i - 1;
            }
            else
            {
                if (startRowIndex != null)
                {
                    // End of a structure detected
                    // Create range from the StartIndex of the first atom in the block
                    // to the Index of the last atom in the block
                    // Calculate robust range from all atoms in the detected block
                    var blockRows = rows.Skip(startRowIndex.Value).Take(i - startRowIndex.Value);
                    var blockAtoms = blockRows.SelectMany(r => r);
                    
                    var startAtomIndex = blockAtoms.Min(a => a.Index);
                    var endAtomIndex = blockAtoms.Max(a => a.Index);
                    
                    _logger.StructureDetected("Table", startAtomIndex, endAtomIndex);
                    zones.Add(new StructuralRange(startAtomIndex, endAtomIndex, "Table"));
                    startRowIndex = null;
                }
            }
        }

        // Catch trailing structure at the end of the page
        if (startRowIndex != null)
        {
            var startAtomIndex = rows[startRowIndex.Value].First().Index;
            var endAtomIndex = rows.Last().Last().Index;
            _logger.StructureDetected("Table", startAtomIndex, endAtomIndex);
            zones.Add(new StructuralRange(startAtomIndex, endAtomIndex, "Table"));
        }

        return zones;
    }

    private bool CheckVerticalAlignment(IEnumerable<GeometricAtom> r1, IEnumerable<GeometricAtom> r2)
    {
        var x1 = r1.Select(a => Math.Round(a.Bounds.X, 0)).ToList();
        var x2 = r2.Select(a => Math.Round(a.Bounds.X, 0)).ToList();

        // Grid Law Logic:
        // 1. Minimum Columns: A table must have at least 2 distinct columns to be considered a grid.
        // 2. Structural Alignment: The X-coordinates of the columns in the current row must match 
        //    the X-coordinates of the previous row within the defined tolerance (AlignmentThreshold).
        //    If they match, we extend the current "Table Zone". If they break, the zone ends.
        
        if (x1.Count < 2 || x2.Count < 2) return false;
        
        // Ensure same number of columns for strict table detection
        if (x1.Count != x2.Count) return false;

        // Check each column alignment
        for(int i=0; i < x1.Count; i++)
        {
            if (Math.Abs(x1[i] - x2[i]) > AlignmentThreshold) return false;
        }

        return true;
    }
}
