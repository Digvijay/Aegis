# Aegis Samples Gallery

This directory contains reference implementations and benchmarks demonstrating the **Geometric Integrity Protocol (GIP)** in action.

## 1. C# Benchmark Sample
**Path:** `samples/Aegis.Sample.Console`

The primary benchmark tool used to validate the superiority of Aegis over traditional chunking.
*   **Purpose**: Compares Aegis against "Fixed-Size Chunking".
*   **Features**:
    *   Loads local PDFs from `samples/random_input`.
    *   Uses `PdfPig` for local geometric atom extraction.
    *   Identifies tables via `GridLawDetector`.
    *   Outputs a side-by-side comparison of "Fragmented" vs "Preserved" results.
*   **Run**: `dotnet run` inside the folder.

## 2. Python Benchmark Sample
**Path:** `samples/aegis_sample_python`

A Pythonic implementation mirroring the C# benchmark to ensure cross-language parity.
*   **Purpose**: Demonstrates that the GIP logic is identical across .NET and Python.
*   **Features**:
    *   Uses `pdfplumber` for geometric extraction.
    *   Links to the `aegis_integrity` package.
    *   Verified to produce 100% identical preservation results as the C# version.
*   **Run**: `python main.py` (after `pip install -r requirements.txt`).

## 3. Azure Producer Sample
**Path:** `samples/Aegis.Sample.AzureProducer`

An enterprise-ready sample demonstrating how to bridge cloud OCR services with Aegis.
*   **Purpose**: Shows how to use the `Aegis.Producer` adapter.
*   **Features**:
    *   Integrates with **Azure AI Document Intelligence**.
    *   Maps Azure's complex "Spans" and "BoundingPolygons" into the Aegis Geometric Manifest.
    *   Demonstrates real-world usage of the `DocumentIntelligenceAdapter`.
*   **Run**: Requires Azure credentials. `dotnet run -- <path-to-pdf>`.

---

### Local Test Data
**Path:** `samples/random_input`
Contains sample PDFs (Invoices, IKEA manuals) used by the benchmark samples for stress-testing table integrity.
