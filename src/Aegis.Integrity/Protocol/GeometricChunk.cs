
namespace Aegis.Integrity.Protocol;

/// <summary>
/// Represents a finalized chunk of text with geometric metadata.
/// </summary>
/// <param name="Content">The text content of the chunk.</param>
/// <param name="StartIndex">The index of the first atom in this chunk.</param>
/// <param name="EndIndex">The index of the last atom in this chunk.</param>
/// <param name="Page">The page number of the first atom.</param>
/// <param name="TokenCount">Total token count.</param>
/// <param name="Discriminator">The reason for the chunk boundary (e.g., "TokenLimit", "Table-Preserved").</param>
public record GeometricChunk(string Content, int StartIndex, int EndIndex, int Page, int TokenCount, string Discriminator);
