
using Xunit;
using Aegis.Integrity.Discovery;
using Aegis.Integrity.Protocol;
using Aegis.Integrity.Pipelines;
using System.Collections.Generic;
using System.Linq;

namespace Aegis.Integrity.Tests;

public class GridLawTests
{
    private readonly GridLawDetector _detector = new(Microsoft.Extensions.Logging.Abstractions.NullLogger<GridLawDetector>.Instance);

    [Fact]
    public void DetectTableZones_WithPerfectlyAlignedTable_ReturnsStructuralRange()
    {
        // ARRANGE: Create 5 lines where X-coordinates are identical (a perfect grid)
        // Line 1: [x:50, x:150], Line 2: [x:50, x:150]...
        var tableAtoms = CreateMockTable(rows: 5, columns: 2, startX: 50.0);

        // ACT
        var results = _detector.DetectTableZones(tableAtoms);

        // ASSERT
        Assert.Single(results);
        Assert.Equal("Table", results[0].Type);
    }

    [Fact]
    public void DetectTableZones_WithRandomParagraphText_ReturnsEmpty()
    {
        // ARRANGE: Create text with varying X-offsets (no alignment)
        var paragraphAtoms = new List<GeometricAtom>
        {
            new("Once", new BoundingBox(50, 100, 10, 10), 1, 1),
            new("upon", new BoundingBox(65, 100, 10, 10), 1, 1),
            new("a",    new BoundingBox(58, 90, 5, 10), 1, 1), // Larger shift (8 > 5)
            new("time", new BoundingBox(80, 90, 10, 10), 1, 1) // Larger shift (15 > 5)
        };

        // ACT
        var results = _detector.DetectTableZones(paragraphAtoms);

        // ASSERT: Should not detect a table because X-variance is too high (and rows count is low)
        Assert.Empty(results);
    }

    [Fact]
    public void IntegrityPipe_ShouldNotSlice_WhenBoundaryHitsTable()
    {
        // ARRANGE
        // Create a 4-row x 2-column table (8 atoms total).
        // The table is defined as a single structural block from index 0 to 7.
        var atoms = CreateMockTable(rows: 4, columns: 2, startX: 50.0); 
        var zones = new List<StructuralRange> { new(0, 7, "Table") };
        var manifest = new GeometricManifest { Atoms = atoms, Structures = zones };
        var pipe = new IntegrityPipe(manifest, Microsoft.Extensions.Logging.Abstractions.NullLogger<IntegrityPipe>.Instance);

        // ACT
        // Request a chunk size (maxTokens: 4) that would naturally split the table in half.
        // The IntegrityPipe should detect the collision with the "Table" structure.
        // Since the split point (4) is past the midpoint (3.5) of the structure (0-7),
        // the Backpressure logic should "Advance" the boundary to the end of the table (index 7).
        var chunks = pipe.GenerateChunks(maxTokens: 4).ToList();

        // ASSERT
        // confirming that the pipe expanded the boundary to preserve structural integrity.
        Assert.Single(chunks);
        Assert.Equal(8, chunks[0].Content.Split(' ', System.StringSplitOptions.RemoveEmptyEntries).Length); 
    }

    private List<GeometricAtom> CreateMockTable(int rows, int columns, double startX)
    {
        var atoms = new List<GeometricAtom>();
        int index = 0;
        
        // Construct atoms using standard PDF coordinate space (Bottom-Left origin).
        // Y-coordinates decrease as we move down the page (Row 0 is visually at the top).
        for (int r = 0; r < rows; r++) // Rows top-to-bottom
        {
            for (int c = 0; c < columns; c++) // Columns left-to-right
            {
                atoms.Add(new GeometricAtom(
                    "Data", 
                    new BoundingBox(startX + (c * 100), 500 - (r * 20), 20, 10), 
                    1, 
                    1) { Index = index++ });
            }
        }
        return atoms;
    }

    [Fact]
    public void IntegrityPipe_ShouldRecede_WhenCollisionIsEarly()
    {
        // ARRANGE
        // Table: 4 rows, 2 columns (8 atoms). Indices: 0-7.
        // We create a PREAMBLE of text before the table to offset indices.
        // Preamble: 2 atoms. Indices 0, 1.
        // Table starts at Index 2, ends at Index 9. (Length 8)
        
        var atoms = new List<GeometricAtom>
        {
            new("Preamble1", new BoundingBox(0,0,0,0), 1, 1) { Index = 0 },
            new("Preamble2", new BoundingBox(0,0,0,0), 1, 1) { Index = 1 }
        };
        
        var tableAtoms = CreateMockTable(4, 2, 50);
        // Retarget indices for table atoms
        foreach(var ta in tableAtoms) { atoms.Add(ta with { Index = ta.Index + 2 }); }
        
        // Structure covers indices 2 to 9.
        var zones = new List<StructuralRange> { new(2, 9, "Table") };
        var manifest = new GeometricManifest { Atoms = atoms, Structures = zones };
        var pipe = new IntegrityPipe(manifest, Microsoft.Extensions.Logging.Abstractions.NullLogger<IntegrityPipe>.Instance);

        // ACT
        // We set maxTokens = 4.
        // Normal split would be at index 4 (after 2 preamble + 2 table atoms).
        // This lands inside the table (start=2, end=9).
        // Split (4) is < Midpoint (2 + (9-2)/2 = 5.5).
        // The pipe should RECEDE to index 2 (Start of table).
        
        var chunks = pipe.GenerateChunks(maxTokens: 4).ToList();

        // ASSERT
        // Chunk 1: Preamble only (2 atoms).
        // Chunk 2: The entire table (8 atoms).
        
        Assert.Equal(2, chunks.Count);
        Assert.Equal("Preamble1 Preamble2", chunks[0].Content); // Receded boundary
        Assert.StartsWith("Data", chunks[1].Content); // Table starts here
    }

    [Fact]
    public void IntegrityPipe_ShouldConsumeOversizedStructure()
    {
        // ARRANGE
        // Table: 10 rows, 1 col (10 atoms). Indices 0-9.
        // Token count = 10.
        // Max Tokens = 5.
        // The table is physically larger than the token window.
        
        var atoms = CreateMockTable(10, 1, 50);
        var zones = new List<StructuralRange> { new(0, 9, "Table") };
        var manifest = new GeometricManifest { Atoms = atoms, Structures = zones };
        var pipe = new IntegrityPipe(manifest, Microsoft.Extensions.Logging.Abstractions.NullLogger<IntegrityPipe>.Instance);

        // ACT
        var chunks = pipe.GenerateChunks(maxTokens: 5).ToList();

        // ASSERT
        // Should produce 1 chunk containing all 10 atoms.
        // Fixed-size would split it into 2 chunks of 5.
        Assert.Single(chunks);
        Assert.Equal(10, chunks[0].Content.Split(' ', System.StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void DetectTableZones_IgnoresMinorMisalignment()
    {
        // ARRANGE
        // Create 2 columns where X coordinates are slightly jittery (scanned PDF artifact).
        // Row 1: X=100, X=200
        // Row 2: X=102, X=198 (Variance is 2.0, within Threshold 5.0)
        
        var atoms = new List<GeometricAtom>
        {
            new("R1C1", new BoundingBox(100, 800, 50, 10), 1, 1) { Index = 0 },
            new("R1C2", new BoundingBox(200, 800, 50, 10), 1, 1) { Index = 1 },
            
            new("R2C1", new BoundingBox(102, 780, 50, 10), 1, 1) { Index = 2 }, // Shift +2
            new("R2C2", new BoundingBox(198, 780, 50, 10), 1, 1) { Index = 3 }  // Shift -2
        };

        // ACT
        var results = _detector.DetectTableZones(atoms);

        // ASSERT
        // Should still be detected as one single table zone covering all 4 atoms.
        Assert.Single(results);
        Assert.Equal(0, results[0].Start);
        Assert.Equal(3, results[0].End);
    }
}
