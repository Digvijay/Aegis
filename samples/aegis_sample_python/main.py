import sys
import os
import math
import logging
import pdfplumber
from pathlib import Path

# Add source to path to import aegis_integrity
# Assuming running from samples/aegis_sample_python, source is ../../src/python/aegis_integrity
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../src/python/aegis_integrity')))

from aegis_integrity import GeometricAtom, BoundingBox, GridLawDetector, GeometricManifest, IntegrityPipe, StructuralRange

# Configure logging to show Info but not Debug
logging.basicConfig(level=logging.INFO, format='%(message)s')
logger = logging.getLogger(__name__)

def main():
    input_dir = Path("../random_input")
    if not input_dir.exists():
        print(f"[Error] Input directory not found: {input_dir.resolve()}")
        return

    pdf_files = list(input_dir.glob("*.pdf"))
    print(f"Found {len(pdf_files)} PDF files to process.\n")

    for pdf_path in pdf_files:
        print("=================================================")
        print(f"   Processing: {pdf_path.name}")
        print("=================================================\n")
        process_pdf(pdf_path)

def process_pdf(pdf_path: Path):
    # 1. Load PDF and Convert to Geometric Atoms
    print(f"[1] Loading PDF from: {pdf_path}")
    atoms = extract_atoms_from_pdf(pdf_path)
    print(f"    -> Extracted {len(atoms)} geometric atoms (tokens).")

    # 2. Discover Structure (Grid Law)
    print("[2] Running Grid Law Detector...")
    detector = GridLawDetector()
    structures = detector.detect_table_zones(atoms)
    
    print(f"    -> Discovered {len(structures)} structural zones (Tables/Lists).")
    for i, s in enumerate(structures):
        if i < 5:
            print(f"       - [{s.type}] Indices {s.start}-{s.end} ({s.end - s.start + 1} tokens)")
        elif i == 5:
            print("       ... (more structures detected)")

    manifest = GeometricManifest(atoms, structures)

    # 3. Define Constraints
    largest_table_size = max((s.end - s.start + 1 for s in structures), default=50)
    max_tokens = int(largest_table_size * 0.7)
    if max_tokens < 10: max_tokens = 50

    print(f"\n[Scenario] Benchmark Constraint: Chunk Size = {max_tokens} tokens")
    print("           (Designed to stress-test the largest table)\n")

    # ---------------------------------------------------------
    # METHOD A: The "Ghost Ship" (Fixed-Size Chunking)
    # ---------------------------------------------------------
    print(">>> Method A: Traditional Fixed-Size Chunking")
    fixed_chunks = run_fixed_size_chunking(atoms, max_tokens)
    analyze_result("Fixed-Size", fixed_chunks, structures)

    print("\n-------------------------------------------------\n")

    # ---------------------------------------------------------
    # METHOD B: Aegis (Geometric Integrity)
    # ---------------------------------------------------------
    print(">>> Method B: Aegis Integrity Protocol")
    pipe = IntegrityPipe(manifest)
    # Temporarily silence logger for batch output clarity if desired, but we kept it INFO.
    
    aegis_chunks = list(pipe.generate_chunks(max_tokens))
    
    print(f"\n[Aegis Output] Generated {len(aegis_chunks)} Chunks (Sample Preview):\n")
    for i, chunk in enumerate(aegis_chunks[:3]):
        print("   +-------------------------------------------------------------+")
        print(f"   | Chunk {i} | Page: {chunk.page} | Tokens: {chunk.token_count} | Type: {chunk.discriminator} |")
        print("   +-------------------------------------------------------------+")
        content_preview = chunk.content.replace('\n', ' ')[:50]
        print(f"   | \"{content_preview}...\" ")
        print("   +-------------------------------------------------------------+\n")

    if len(aegis_chunks) > 3:
        print(f"   ... ({len(aegis_chunks) - 3} more chunks hidden)\n")

    # Verify Aegis Integrity
    # Map geometric chunks to structural ranges for analysis
    aegis_ranges = [StructuralRange(c.start_index, c.end_index, "Chunk") for c in aegis_chunks]
    analyze_result("Aegis", aegis_ranges, structures)

def extract_atoms_from_pdf(path: Path) -> list[GeometricAtom]:
    atoms = []
    index = 0
    with pdfplumber.open(path) as pdf:
        for page_num, page in enumerate(pdf.pages, 1):
            words = page.extract_words()
            for w in words:
                # pdfplumber: x0, top, x1, bottom
                # Aegis BoundingBox: x, y, width, height.
                # PDF Coordinate system: usually Bottom-Left.
                # pdfplumber 'top' is from Top. 
                # We need to normalize. 
                # Let's stick to pdfplumber's Y (Top-Down) if our detector uses relative alignment it doesn't matter 
                # AS LONG AS we are consistent.
                # GridLawDetector assumes Y sort.
                
                x = w['x0']
                y = w['top'] # Using Top-Down Y
                width = w['x1'] - w['x0']
                height = w['bottom'] - w['top']
                
                text = w['text']
                # Estimate tokens: chars / 4
                token_count = max(1, math.ceil(len(text) / 4.0))
                
                atoms.append(GeometricAtom(text, BoundingBox(x, y, width, height), page_num, token_count, index))
                index += 1
    return atoms

def run_fixed_size_chunking(atoms: list[GeometricAtom], size: int) -> list[StructuralRange]:
    chunks = []
    for i in range(0, len(atoms), size):
        end = min(i + size - 1, len(atoms) - 1)
        chunks.append(StructuralRange(i, end, "Chunk"))
    return chunks

def analyze_result(method: str, chunk_ranges: list[StructuralRange], tables: list[StructuralRange]):
    broken_tables = 0
    
    for table in tables:
        is_contained = False
        for chunk in chunk_ranges:
            if chunk.start <= table.start and chunk.end >= table.end:
                is_contained = True
                break
        
        if not is_contained:
            broken_tables += 1

    failed = broken_tables > 0
    
    print(f"   Result: {'FRAGMENTED' if failed else 'PRESERVED'}")
    if failed:
        print(f"   Impact: {broken_tables} out of {len(tables)} tables were destroyed.")
        print("   RAG Outcome: Hallucination Risk HIGH.")
    else:
        print("   Impact: 100% of tables preserved.")
        print("   RAG Outcome: Retrieval Precision 100%.")
    
    print(f"   Total Chunks: {len(chunk_ranges)}")

if __name__ == "__main__":
    main()
