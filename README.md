# Aegis: The Geometric Integrity Protocol (GIP)

**A cross-platform Geometric Integrity Protocol (GIP) for RAG that prevents structural fragmentation of tables and lists using spatial Grid Laws. A unified standard for .NET and Python.**

Aegis is more than just a library; it is a **Deterministic Ingestion Standard** for enterprise RAG systems. It solves the "Structural Fragmentation" problem by utilizing Spatial Grid Laws to ensure that tables, lists, and multi-column layouts are never "shredded" during the chunking process, regardless of whether your stack is built on .NET or Python.

## Key Features

*   **Deterministic Ingestion**: Moves beyond "best-effort" string splitting to coordinate-based structural preservation. **This creates an auditable Source of Truth.**
*   **Legacy-to-Edge Bridge**: Multi-targets `.NET Standard 2.0` through `.NET 10` to allow modern RAG architectures to interface directly with legacy enterprise systems.
*   **Grid Law Discovery**: Automatically identifies tabular structures using X-coordinate variance—no OCR or AI models required.
*   **Elastic Chunking**: Dynamically adjusts chunk boundaries to fit the document's visual "gaps."
*   **Cloud-Native Pipe**: Built on `IAsyncEnumerable` and `Span<T>` for high-throughput, low-memory streaming in serverless environments (Azure Functions).
*   **The Unified Standard**: Includes a Python wrapper (`aegis_integrity`) that binds to the exact same logic, allowing Data Science (Python) and Backend Engineering (C#) to share a single, verifiable ingestion protocol.

## Components

- **Aegis.Integrity**: The core C# engine implementing the Grid Law Detector and Integrity Pipe.
- **Aegis.Producer**: Adapter for ingesting Azure AI Document Intelligence results (`AnalyzeResult`) into Aegis Manifests.
- **Aegis.Python**: Python wrapper (`aegis_integrity.py`) to consume Geometric Manifests.
- **Aegis.Visualizer**: WinUI 3 tool to visualize "No-Cut Zones" and verify chunk boundaries (Source code included).
- **Aegis.Sample.Console**: A benchmark console app that proves "Structural Fragmentation" using C#.
- **Aegis.Sample.BenchmarkJudge**: An LLM-as-a-Judge benchmark (Python) that provides empirical scores for structural fidelity and semantic coherence vs. industry splitters.

## Quick Start (C#)

```csharp
using Aegis.Integrity;
using Aegis.Integrity.Pipelines;
using Microsoft.Extensions.Logging; // Required for LoggerFactory

// 1. Initialize Aegis
using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var engine = new AegisEngine(loggerFactory);

// 2. Stream Verified Chunks
using var pdfStream = File.OpenRead("document.pdf"); // Assuming 'document.pdf' exists
await foreach (var chunk in engine.StreamVerifiedChunksAsync(pdfStream, maxTokens: 512))
{
    Console.WriteLine($"Chunk (Page {chunk.Page}): {chunk.Content}");
    
    // Push to Vector Store...
}
```

For more detailed examples, see our **[Samples Gallery](docs/SAMPLES.md)**.

## RAG Integration Pattern

Aegis metadata is designed to be mapped directly into your Vector Store (e.g., Azure AI Search, Pinecone, or Weaviate) to enable high-precision citations and debugging.

### Recommended Schema Mapping

| Aegis Property | Vector Store Field | Purpose |
| :--- | :--- | :--- |
| `chunk.Content` | `content` (Searchable) | The semantically preserved text for LLM context. |
| `chunk.Page` | `metadata_page` (Filterable) | Enables "Link to Source" in your UI. |
| `chunk.TokenCount` | `metadata_tokens` | Helps manage LLM context window limits. |
| `chunk.Discriminator`| `metadata_chunk_type`| Audit trail (e.g., `Preserved-Table`, `TokenLimit`). |

### C# Example: Pushing to AI Search

```csharp
var chunks = engine.StreamVerifiedChunksAsync(pdfStream, 512);

await foreach (var chunk in chunks)
{
    var document = new {
        id = Guid.NewGuid().ToString(),
        content = chunk.Content,
        metadata = new {
            page = chunk.Page,
            tokens = chunk.TokenCount,
            integrity_reason = chunk.Discriminator // e.g., "Preserved-Table"
        }
    };
    
    // upload to search index...
}
```

For more detailed examples, see our **[Samples Gallery](docs/SAMPLES.md)**.

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

