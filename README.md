
# Aegis: The Geometric Integrity Protocol (GIP)

Aegis is a high-performance .NET library designed to solve the "Structural Fragmentation" problem in Retrieval-Augmented Generation (RAG). By utilizing Spatial Grid Laws, it ensures that tables, lists, and multi-column layouts are never "shredded" during the document chunking process.

## Key Features

*   **Deterministic Ingestion**: Moves beyond "best-effort" string splitting to coordinate-based structural preservation. **This creates an auditable Source of Truth.**
*   **Legacy-to-Edge Bridge**: Multi-targets `.NET Standard 2.0` through `.NET 10` to allow modern RAG architectures to interface directly with legacy enterprise systems.
*   **Grid Law Discovery**: Automatically identifies tabular structures using X-coordinate variance—no OCR or AI models required.
*   **Elastic Chunking**: Dynamically adjusts chunk boundaries to fit the document's visual "gaps."
*   **Cloud-Native Pipe**: Built on `IAsyncEnumerable` and `Span<T>` for high-throughput, low-memory streaming in serverless environments (Azure Functions).
*   **The Unified Standard**: Includes a Python wrapper (`aegis_integrity`) that binds to the exact same logic, allowing Data Science (Python) and Backend Engineering (.NET) to share a single, verifiable ingestion protocol.

## Components

- **Aegis.Integrity**: The core C# engine implementing the Grid Law Detector and Integrity Pipe.
- **Aegis.Producer**: Adapter for ingesting Azure AI Document Intelligence results (`AnalyzeResult`) into Aegis Manifests.
- **Aegis.Python**: Python wrapper (`aegis_integrity.py`) to consume Geometric Manifests.
- **Aegis.Visualizer**: WinUI 3 tool to visualize "No-Cut Zones" and verify chunk boundaries (Source code included).
- **Aegis.Sample.Console**: A benchmark console app that proves "Structural Fragmentation" by comparing fixed-size chunking against Aegis.

## Quick Start (C#)

```csharp
using Aegis.Integrity;
using Aegis.Integrity.Pipelines;

// 1. Initialize the Engine
var aegis = new AegisEngine();

// 2. Stream verified chunks directly from a PDF
using var stream = File.OpenRead("document.pdf");
await foreach (var chunk in aegis.StreamVerifiedChunksAsync(stream, maxTokens: 512))
{
    Console.WriteLine($"[Chunk Length: {chunk.Content.Length}] Page: {chunk.Page}");
    Console.WriteLine(chunk.Content);
    
    // Push to Vector Store...
}
```

## Quick Start (Python)

Aegis provides a first-class Python experience for Data Science workflows.

```bash
pip install ./src/python
```

```python
import pdfplumber
from aegis_integrity import GridLawDetector, GeometricManifest, IntegrityPipe, GeometricAtom, BoundingBox

# 1. Extract Atoms (using pdfplumber)
with pdfplumber.open("document.pdf") as pdf:
    atoms = []
    for page in pdf.pages:
        for word in page.extract_words():
            atoms.append(GeometricAtom(
                text=word['text'],
                bounds=BoundingBox(word['x0'], word['top'], word['x1']-word['x0'], word['bottom']-word['top']),
                page=page.page_number,
                token_count=len(word['text']) // 4 + 1,
                index=len(atoms)
            ))

# 2. Run Integrity Chain
detector = GridLawDetector()
manifest = GeometricManifest(atoms, detector.detect_table_zones(atoms))
pipe = IntegrityPipe(manifest)

# 3. Generate Verified Chunks
for chunk in pipe.generate_chunks(max_tokens=512):
    print(f"Chunk from Page {chunk.page}: {chunk.content[:50]}...")
```


## How It Works: The Grid Law

Aegis identifies "No-Cut Zones" by analyzing the Vertical Rhythm of the document.

1.  **Spatial Audit**: Aegis peeks at the PDF's internal coordinate map to find where every word sits on the (x,y) plane.
2.  **Variance Detection**: If several lines of text share identical X offsets with low standard deviation (σ), Aegis identifies a Grid Invariant (a table or column).
3.  **Boundary Negotiation**: When a chunk hits its token limit inside a Grid Invariant, the pipe applies Backpressure—either shrinking or stretching the chunk to keep the structure whole.

## Whitepaper

For a deep dive into the math and the "Grid Law" theory, please refer to the **[Geometric Integrity Whitepaper](docs/whitepaper.md)** included in this repository.

## Testing

Run the unit tests to verify the Grid Law logic:

```bash
dotnet test
```

## Structure

```
src/
  Aegis.Integrity/       # Core Logic
  Aegis.Producer/        # Azure Adapter
  Aegis.Integrity.Tests/ # Unit Tests
  Aegis.Visualizer/      # WinUI 3 Visualization Tool
  python/
    aegis_integrity/     # Python Wrapper
docs/
  whitepaper.md          # Technical Specification
```

## Acknowledgements

*   **UglyToad**: This project is built upon the excellent [PdfPig](https://github.com/UglyToad/PdfPig) library for PDF parsing. We stand on the shoulders of giants.

