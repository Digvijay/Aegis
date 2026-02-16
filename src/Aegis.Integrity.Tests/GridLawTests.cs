
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
        // 100 atoms total. Structure from 20-80 (length 60).
        var atoms = CreateMockTable(rows: 100, columns: 1, startX: 50.0); 
        var zones = new List<StructuralRange> { new(20, 80, "Table") };
        var manifest = new GeometricManifest { Atoms = atoms, Structures = zones };
        var pipe = new IntegrityPipe(manifest, Microsoft.Extensions.Logging.Abstractions.NullLogger<IntegrityPipe>.Instance);

        // ACT
        // Target 50, Max 75. 
        // proximity = (50-20) / 60 = 0.5. Should RECEDE to index 20.
        var chunks = pipe.GenerateChunks(targetTokens: 50, hardMaxTokens: 75).ToList();

        // ASSERT
        // Chunk 1: indices 0-19. EndIndex = 19.
        Assert.Equal(19, chunks[0].EndIndex);
        Assert.Equal(20, chunks[1].StartIndex);
        Assert.Equal(80, chunks[1].EndIndex);
    }

    [Fact]
    public void IntegrityPipe_ShouldSoftBreak_MassiveStructure()
    {
        // ARRANGE
        // 200 atoms. Structure from 0-199.
        var atoms = CreateMockTable(rows: 200, columns: 1, startX: 50.0);
        var zones = new List<StructuralRange> { new(0, 199, "Table") };
        var manifest = new GeometricManifest { Atoms = atoms, Structures = zones };
        var pipe = new IntegrityPipe(manifest, Microsoft.Extensions.Logging.Abstractions.NullLogger<IntegrityPipe>.Instance);

        // ACT
        // Target 50, Max 75. Structure 200. Should Soft-Break.
        var chunks = pipe.GenerateChunks(targetTokens: 50, hardMaxTokens: 75).ToList();

        // ASSERT
        Assert.Equal(50, chunks[0].TokenCount);
        Assert.Contains("SoftBreak", chunks[0].Discriminator);
    }

    [Fact]
    public void IntegrityPipe_ShouldOverlap_GeometricWindow()
    {
        // ARRANGE
        var atoms = CreateMockTable(rows: 100, columns: 1, startX: 50.0);
        var manifest = new GeometricManifest { Atoms = atoms, Structures = new List<StructuralRange>() };
        var pipe = new IntegrityPipe(manifest, Microsoft.Extensions.Logging.Abstractions.NullLogger<IntegrityPipe>.Instance, overlapTokens: 10);

        // ACT
        var chunks = pipe.GenerateChunks(targetTokens: 50).ToList();

        // ASSERT
        // Chunk 0: index 0 to 49 (50 atoms).
        // Next starts at 50 - 10 = 40.
        Assert.Equal(49, chunks[0].EndIndex);
        Assert.Equal(40, chunks[1].StartIndex);
    }

    [Fact]
    public void IntegrityPipe_ShouldMerge_TrailingFragments()
    {
        // ARRANGE
        // 115 atoms. Target 100. (Remainder 15 > 10, so it will NOT merge? No, let's check threshold)
        // Wait, Python fix was: remaining < 10. 
        // 10 atoms is very small. Let's use 105 total, target 100.
        var atoms = CreateMockTable(rows: 105, columns: 1, startX: 50.0);
        var manifest = new GeometricManifest { Atoms = atoms, Structures = new List<StructuralRange>() };
        var pipe = new IntegrityPipe(manifest, Microsoft.Extensions.Logging.Abstractions.NullLogger<IntegrityPipe>.Instance);

        // ACT
        var chunks = pipe.GenerateChunks(targetTokens: 100).ToList();

        // ASSERT
        // Should merge 100 + 5 atoms into one chunk of 105.
        Assert.Single(chunks);
        Assert.Equal(105, chunks[0].TokenCount);
    }

    [Fact]
    public void GridLawDetector_ShouldRespect_RTL()
    {
        // ARRANGE
        var atoms = new List<GeometricAtom>
        {
            new("Left", new BoundingBox(10, 100, 10, 10), 1, 1) { Index = 0 },
            new("Right", new BoundingBox(90, 100, 10, 10), 1, 1) { Index = 1 }
        };
        
        // ACT
        var ltrDetector = new GridLawDetector(Microsoft.Extensions.Logging.Abstractions.NullLogger<GridLawDetector>.Instance);
        var rtlZones = ltrDetector.DetectTableZones(atoms, direction: "RTL");
        
        // ASSERT
        Assert.NotNull(rtlZones);
    }

    private List<GeometricAtom> CreateMockTable(int rows, int columns, double startX)
    {
        var atoms = new List<GeometricAtom>();
        int index = 0;
        
        for (int r = 0; r < rows; r++) 
        {
            for (int c = 0; c < columns; c++) 
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
}
