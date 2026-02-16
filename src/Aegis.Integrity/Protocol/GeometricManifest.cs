
using System.Collections.Generic;

namespace Aegis.Integrity.Protocol;

/// <summary>
/// The serialized manifest containing the document's geometric and structural data.
/// This acts as the contract between the ingestion (Producer) and the chunking (Consumer) phases.
/// </summary>
public class GeometricManifest
{
    private List<List<StructuralRange>>? _indexMap;

    /// <summary>
    /// The stream of atoms (words/tokens) in the document.
    /// </summary>
    public List<GeometricAtom> Atoms { get; set; } = new();

    /// <summary>
    /// The identified structural zones that should be preserved.
    /// </summary>
    public List<StructuralRange> Structures { get; set; } = new();

    /// <summary>
    /// Pre-computes the structural index map for O(1) lookups.
    /// Must be called after Atoms and Structures are fully populated.
    /// </summary>
    public void FinalizeMapping()
    {
        _indexMap = new List<List<StructuralRange>>(Atoms.Count + 1);
        for (int i = 0; i <= Atoms.Count; i++) _indexMap.Add(new List<StructuralRange>());

        foreach (var s in Structures)
        {
            int safeStart = Math.Max(0, Math.Min(s.Start, Atoms.Count - 1));
            int safeEnd = Math.Max(0, Math.Min(s.End, Atoms.Count - 1));

            for (int i = safeStart; i <= safeEnd; i++)
            {
                _indexMap[i].Add(s);
            }
        }
    }

    /// <summary>
    /// Retrieves all structures containing the atom at the specified index.
    /// </summary>
    public IReadOnlyList<StructuralRange> GetStructuresAt(int index)
    {
        if (_indexMap == null) FinalizeMapping();
        return (index >= 0 && index < _indexMap!.Count) ? _indexMap[index] : new List<StructuralRange>();
    }
}
