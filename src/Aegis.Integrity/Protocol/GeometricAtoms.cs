
namespace Aegis.Integrity.Protocol;

/// <summary>
/// Represents the smallest unit of geometric data (a word or token) with its spatial coordinates.
/// </summary>
/// <param name="Text">The text content of the atom.</param>
/// <param name="Bounds">The bounding box coordinates [X, Y, Width, Height].</param>
/// <param name="Page">The page number where this atom resides.</param>
/// <param name="TokenCount">Estimated or actual token count for this atom.</param>
public record GeometricAtom(string Text, BoundingBox Bounds, int Page, int TokenCount)
{
    /// <summary>
    /// The index of this atom in the original document sequence.
    /// </summary>
    public int Index { get; init; }
}

/// <summary>
/// Represents a bounding box in 2D space.
/// </summary>
/// <param name="X">X coordinate (usually left).</param>
/// <param name="Y">Y coordinate (usually bottom-left depending on PDF coordinate system).</param>
/// <param name="Width">Width of the box.</param>
/// <param name="Height">Height of the box.</param>
public record BoundingBox(double X, double Y, double Width, double Height);

/// <summary>
/// Represents a logical grouping of atoms that should not be split (e.g., a table row or paragraph).
/// </summary>
public class StructuralRange
{
    public int Start { get; }
    public int End { get; }
    public string Type { get; }

    public StructuralRange(int start, int end, string type)
    {
        Start = start;
        End = end;
        Type = type;
    }
}
