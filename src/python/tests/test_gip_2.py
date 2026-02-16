import sys
import os
import unittest
from typing import List

# Setup path to internal source
sys.path.insert(0, os.path.abspath('src/python'))
from aegis_integrity.aegis_integrity import (
    GeometricAtom, BoundingBox, GridLawDetector, GeometricManifest, IntegrityPipe, StructuralRange
)

def create_mock_atom(index: int, text: str = "word", x: float = 0, y: float = 0):
    return GeometricAtom(
        text=text,
        bounds=BoundingBox(x, y, 10, 10),
        page=1,
        token_count=1,
        index=index
    )

class TestGIP2(unittest.TestCase):
    def test_dual_token_constraints_preserves_small_structure(self):
        # 100 atoms total. Structure from 20-80 (length 60).
        atoms = [create_mock_atom(i) for i in range(100)]
        structures = [StructuralRange(20, 80, "Table")]
        manifest = GeometricManifest(atoms, structures)
        pipe = IntegrityPipe(manifest)
        
        # Target 50, Max 75. 
        # Token limit hits at index 50 (middle of structure).
        # proximity = (50-20) / 60 = 0.5. 
        # Should RECEDE to index 20.
        chunks = list(pipe.generate_chunks(target_tokens=50, hard_max_tokens=75))
        
        self.assertEqual(chunks[0].end_index, 19)
        self.assertEqual(chunks[1].start_index, 20)
        self.assertEqual(chunks[1].end_index, 80)

    def test_dual_token_constraints_splits_massive_structure(self):
        # 200 atoms. Structure from 0-199 (length 200).
        atoms = [create_mock_atom(i) for i in range(200)]
        structures = [StructuralRange(0, 199, "Table")]
        manifest = GeometricManifest(atoms, structures)
        pipe = IntegrityPipe(manifest)
        
        # Target 50, Max 75.
        chunks = list(pipe.generate_chunks(target_tokens=50, hard_max_tokens=75))
        
        self.assertEqual(chunks[0].token_count, 50)
        self.assertTrue("SoftBreak" in chunks[0].discriminator)

    def test_geometric_overlap(self):
        atoms = [create_mock_atom(i) for i in range(100)]
        manifest = GeometricManifest(atoms, [])
        # 10 tokens overlap
        pipe = IntegrityPipe(manifest, overlap_tokens=10)
        
        chunks = list(pipe.generate_chunks(target_tokens=50))
        
        # Chunk 0: atoms 0-49 (end=50)
        self.assertEqual(chunks[0].end_index, 49)
        # Chunk 1: starts at 50 - 10 = 40.
        self.assertEqual(chunks[1].start_index, 40)

    def test_trailing_fragment_merge(self):
        # 15 atoms. Target 10.
        # Chunk 1 gets 10 atoms. 5 atoms remain.
        # Since 5 < 10 (density fix threshold), it should be swallowed by Chunk 1.
        atoms = [create_mock_atom(i) for i in range(15)]
        manifest = GeometricManifest(atoms, [])
        pipe = IntegrityPipe(manifest)
        
        chunks = list(pipe.generate_chunks(target_tokens=10))
        
        # Should result in 1 large chunk instead of [10, 5]
        self.assertEqual(len(chunks), 1)
        self.assertEqual(chunks[0].token_count, 15)

    def test_rtl_globalization(self):
        a1 = create_mock_atom(0, "Left", x=10, y=100)
        a2 = create_mock_atom(1, "Middle", x=50, y=100)
        a3 = create_mock_atom(2, "Right", x=90, y=100)
        
        detector = GridLawDetector()
        # Verify it runs without error with RTL direction
        zones = detector.detect_table_zones([a1, a2, a3], direction="RTL")
        self.assertIsInstance(zones, list)

if __name__ == "__main__":
    unittest.main()
