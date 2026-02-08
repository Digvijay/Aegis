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
        self.atoms = atoms
        self.structures = structures


class GridLawDetector:
    """
    Detects tabular structures by analyzing spatial frequency and alignment of atoms.
    No OCR required. Pure coordinate math.
    """
    ALIGNMENT_THRESHOLD = 5.0  # Points (variance allowed)

    def detect_table_zones(self, atoms: List[GeometricAtom]) -> List[StructuralRange]:
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
        sorted_y = sorted(rows_dict.keys(), reverse=True)
        rows = [rows_dict[y] for y in sorted_y]
        
        # Ensure atoms in each row are sorted X-ascending (Left to Right)
        for r in rows:
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
    def __init__(self, manifest: GeometricManifest):
        self.manifest = manifest

    def generate_chunks(self, max_tokens: int) -> Generator[GeometricChunk, None, None]:
        """
        Generates text chunks that respect the geometric integrity of the document.
        :param max_tokens: Maximum tokens per chunk.
        :return: Generator yielding GeometricChunk objects.
        """
        cursor = 0
        total_atoms = len(self.manifest.atoms)
        chunk_idx = 0

        while cursor < total_atoms:
            # 1. Proposed cut point
            end = self._find_token_boundary(cursor, max_tokens)
            reason = "TokenLimit"
            
            # 2. Check for Structural Collision
            collision = next((s for s in self.manifest.structures if s.start < end < s.end), None)
            
            if collision:
                # 3. Negotiate Boundary (Backpressure)
                midpoint = collision.start + ((collision.end - collision.start) / 2)
                
                if end > midpoint:
                    # Advance: consume whole structure
                    logger.debug(f"Backpressure Advance at atom {end} for structure type {collision.type}")
                    end = collision.end + 1
                    reason = f"Preserved-{collision.type}"
                else:
                    # Recede: back off to start
                    logger.debug(f"Backpressure Recede at atom {end}, backing off to {collision.start}")
                    end = collision.start
                    reason = "Backpressure-Recede"
                    
                    # Prevent infinite loop if structure > max_tokens
                    if end <= cursor:
                        logger.warning(f"Oversized structure at {cursor}, being forced to consume.")
                        end = collision.end + 1
                        reason = f"Oversize-{collision.type}"
            
            end = min(end, total_atoms)
            
            # 4. Emit Chunk
            chunk_atoms = self.manifest.atoms[cursor:end]
            content = " ".join(atom.text for atom in chunk_atoms)
            token_count = sum(a.token_count for a in chunk_atoms)
            start_idx = chunk_atoms[0].index
            end_idx = chunk_atoms[-1].index
            page = chunk_atoms[0].page

            logger.info(f"Generated chunk {chunk_idx} with {len(chunk_atoms)} atoms.")
            chunk_idx += 1
            yield GeometricChunk(content, start_idx, end_idx, page, token_count, reason)
            
            # 5. Advance
            cursor = end

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
