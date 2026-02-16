import logging
import math
from dataclasses import dataclass
from typing import List, Generator, Optional

logger = logging.getLogger(__name__)

@dataclass
class BoundingBox:
    x: float
    y: float
    width: float
    height: float

@dataclass
class GeometricAtom:
    text: str
    bounds: BoundingBox
    page: int
    token_count: int
    index: int

@dataclass
class StructuralRange:
    start: int
    end: int
    type: str

class GeometricManifest:
    def __init__(self, atoms: List[GeometricAtom], structures: List[StructuralRange]):
        """
        Maintains a pre-computed Interval Map for O(1) structural verification.
        """
        if atoms is None:
            raise ValueError("GeometricManifest requires a non-null atoms list.")
        
        self.atoms = atoms
        self.structures = structures or []
        
        # GIP 2.3 Sovereign Hardening: Pre-compute Structural Index Map
        # Maps atom index -> List[StructuralRange] for O(1) logical lookups
        self._index_map = [[] for _ in range(len(atoms) + 1)]
        for s in self.structures:
            # Ensure boundaries are safe
            safe_start = max(0, min(s.start, len(atoms) - 1))
            safe_end = max(0, min(s.end, len(atoms) - 1))
            for i in range(safe_start, safe_end + 1):
                self._index_map[i].append(s)

    def get_structures_at(self, atom_index: int) -> List[StructuralRange]:
        """O(1) lookup for structures containing the given atom."""
        if 0 <= atom_index < len(self._index_map):
            return self._index_map[atom_index]
        return []


class GridLawDetector:
    """
    Detects tabular structures by analyzing spatial frequency and alignment of atoms.
    No OCR required. Pure coordinate math.
    """
    ALIGNMENT_THRESHOLD = 5.0  # Points (variance allowed)

    def detect_table_zones(self, atoms: List[GeometricAtom], direction: str = "LTR") -> List[StructuralRange]:
        """
        Detects tabular structures. 
        :param direction: "LTR" (Left-to-Right) or "RTL" (Right-to-Left)
        """
        if not atoms:
            return []

        logger.info(f"Discovery Started for Page {atoms[0].page} with {len(atoms)} atoms.")

        zones = []
        
        # Group atoms into horizontal lines (clustered by Y coordinate)
        # Python's groupby needs sorted data, but here we want to cluster by rounded Y.
        # We can use a dictionary for grouping.
        rows_dict = {}
        for atom in atoms:
            y_key = round(atom.bounds.y, 1)
            if y_key not in rows_dict:
                rows_dict[y_key] = []
            rows_dict[y_key].append(atom)

        # Sort rows top to bottom (assuming Y grows upwards in PDF usually, but let's check PdfPlumber. 
        # PdfPlumber: Y grows DOWNLOADS usually? No, PDF standard is Bottom-Left origin. 
        # But PdfPlumber might normalize. C# PdfPig uses Bottom-Left. 
        # Let's assume generic Y sorting. If Top-Left logic (Y increases downwards), we sort by Y.
        # If Bottom-Left (Y increases upwards), we sort descending.
        # SAFE BET: Sort by Y descending (Top to Bottom for PDF standard coordinates).
        # Sort rows top to bottom
        sorted_y = sorted(rows_dict.keys(), reverse=True)
        rows = [rows_dict[y] for y in sorted_y]
        
        # Ensure atoms in each row are sorted by reading order
        for r in rows:
            if direction == "RTL":
                r.sort(key=lambda a: a.bounds.x, reverse=True)
            else:
                r.sort(key=lambda a: a.bounds.x)

        start_row_index: Optional[int] = None

        for i in range(1, len(rows)):
            # Compare logical "columns" (X-starts) of current row vs previous row
            is_aligned = self._check_vertical_alignment(rows[i - 1], rows[i])

            if is_aligned:
                if start_row_index is None:
                    start_row_index = i - 1
            else:
                if start_row_index is not None:
                    # End of a structure detected
                    self._add_zone(zones, rows, start_row_index, i)
                    start_row_index = None

        # Catch trailing structure
        if start_row_index is not None:
            self._add_zone(zones, rows, start_row_index, len(rows))

        return zones

    def _add_zone(self, zones, rows, start_row_index, end_row_index):
        # Calculate robust range from all atoms in the detected block
        block_rows = rows[start_row_index:end_row_index]
        block_atoms = [atom for row in block_rows for atom in row]
        
        if not block_atoms: 
            return

        start_atom_index = min(a.index for a in block_atoms)
        end_atom_index = max(a.index for a in block_atoms)
        
        logger.info(f"Structure Detected (Table): Atoms {start_atom_index}-{end_atom_index}")
        zones.append(StructuralRange(start_atom_index, end_atom_index, "Table"))

    def _check_vertical_alignment(self, r1: List[GeometricAtom], r2: List[GeometricAtom]) -> bool:
        x1 = [round(a.bounds.x, 0) for a in r1]
        x2 = [round(a.bounds.x, 0) for a in r2]

        # Grid Law Logic:
        # 1. Minimum Columns: Must have >= 2 columns
        if len(x1) < 2 or len(x2) < 2:
            return False
            
        # 2. Column Count Match
        if len(x1) != len(x2):
            return False

        # 3. Structural Alignment (X coordinates match within threshold)
        for i in range(len(x1)):
            if abs(x1[i] - x2[i]) > self.ALIGNMENT_THRESHOLD:
                return False

        return True


@dataclass
class GeometricChunk:
    content: str
    start_index: int
    end_index: int
    page: int
    token_count: int
    discriminator: str

class IntegrityPipe:
    """Enterprise-grade Geometric Integrity Pipeline."""
    def __init__(self, manifest: GeometricManifest, overlap_tokens: int = 0):
        if not manifest:
            raise ValueError("IntegrityPipe requires a valid GeometricManifest.")
        self.manifest = manifest
        self.overlap_tokens = max(0, overlap_tokens)

    def generate_chunks(self, target_tokens: int, hard_max_tokens: Optional[int] = None) -> Generator[GeometricChunk, None, None]:
        """
        Generates text chunks using optimized O(1) structural verification.
        """
        if target_tokens <= 0:
            raise ValueError("target_tokens must be positive")

        if hard_max_tokens is None:
            hard_max_tokens = int(target_tokens * 1.2) # Conservative preference

        soft_break_threshold = 0.5 
        cursor = 0
        total_atoms = len(self.manifest.atoms)
        chunk_idx = 0

        while cursor < total_atoms:
            # 1. Proposed cut point based on Target
            end = self._find_token_boundary(cursor, target_tokens)
            reason = "TargetReached"
            
            # 2. GIP 2.3 Optimized Structural Collision Check (O(1))
            # Peek at the boundary atom's structural associations
            structures = self.manifest.get_structures_at(end)
            collision = structures[0] if structures else None
            
            if collision:
                # GIP 2.0: Soft-Break & Target Logic
                structure_size = collision.end - collision.start
                proximity_to_end = (end - collision.start) / max(1, structure_size)
                
                if structure_size > hard_max_tokens or proximity_to_end > soft_break_threshold:
                    if structure_size > hard_max_tokens:
                        logger.info(f"Soft-Break for oversized {collision.type}")
                        reason = f"SoftBreak-{collision.type}"
                    else:
                        end = collision.end + 1
                        reason = f"Preserved-{collision.type}"
                else:
                    end = collision.start
                    reason = "Backpressure-Recede"
                    
                    if end <= cursor:
                        end = self._find_token_boundary(cursor, target_tokens)
                        reason = f"ForcedSplit-{collision.type}"
            
            end = min(end, total_atoms)
            
            # 4. Emit Chunk
            chunk_atoms = self.manifest.atoms[cursor:end]
            if not chunk_atoms:
                break
                
            # Density Fix: Merging trailing fragments
            # Use a percentage of target tokens rather than a hard constant (10)
            density_threshold = max(2, int(target_tokens * 0.2)) 
            remaining = total_atoms - end
            if 0 < remaining < density_threshold: 
                end = total_atoms
                chunk_atoms = self.manifest.atoms[cursor:end]

            # Semantic Anchoring: Enhanced Contextual Ancestry
            page_val = chunk_atoms[0].page
            
            # Identify structures involving this chunk (O(1) lookup)
            s_types = []
            for atomic_idx in [cursor, end - 1]:
                at_structures = self.manifest.get_structures_at(atomic_idx)
                for s in at_structures:
                    if s.type not in s_types:
                        s_types.append(s.type)
            
            markers = [f"[Page {page_val}]"]
            for t in s_types:
                markers.append(f"[{t}]")
            
            prefix = " ".join(markers) + " " if markers else ""
            content = prefix + " ".join(atom.text for atom in chunk_atoms)
            
            token_count = sum(a.token_count for a in chunk_atoms)
            start_idx = chunk_atoms[0].index
            end_idx = chunk_atoms[-1].index

            logger.info(f"Aegis Chunk {chunk_idx}: {token_count} tokens ({reason})")
            chunk_idx += 1
            yield GeometricChunk(content, start_idx, end_idx, page_val, token_count, reason)
            
            # GIP 2.0: Geometric Overlap
            # Move cursor back by overlap amount, but ensure progress
            if self.overlap_tokens > 0:
                new_cursor = self._find_token_overlap(end, self.overlap_tokens)
                cursor = max(cursor + 1, new_cursor) # Ensure at least 1 atom progress
            else:
                cursor = end

    def _find_token_overlap(self, end: int, overlap_limit: int) -> int:
        """Finds the index to start the next chunk for overlap."""
        current_tokens = 0
        i = end - 1
        while i >= 0:
            current_tokens += self.manifest.atoms[i].token_count
            if current_tokens > overlap_limit:
                return i + 1
            i -= 1
        return 0

    def _find_token_boundary(self, start: int, limit: int) -> int:
        current_tokens = 0
        i = start
        while i < len(self.manifest.atoms):
            current_tokens += self.manifest.atoms[i].token_count
            if current_tokens > limit:
                break
            i += 1
        
        if i == start and start < len(self.manifest.atoms):
            return start + 1
            
        return i
