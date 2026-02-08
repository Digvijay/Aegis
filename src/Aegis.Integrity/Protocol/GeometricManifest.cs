
using System.Collections.Generic;

namespace Aegis.Integrity.Protocol;

/// <summary>
/// The serialized manifest containing the document's geometric and structural data.
/// This acts as the contract between the ingestion (Producer) and the chunking (Consumer) phases.
/// </summary>
public class GeometricManifest
{
    /// <summary>
    /// The stream of atoms (words/tokens) in the document.
    /// </summary>
    public List<GeometricAtom> Atoms { get; set; } = new();

    /// <summary>
    /// The identified structural zones that should be preserved.
    /// </summary>
    public List<StructuralRange> Structures { get; set; } = new();
}
