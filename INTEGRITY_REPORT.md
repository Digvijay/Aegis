# Aegis GIP: High-Fidelity RAG Audit
*Evaluation Methodology: Curated Golden Set + RAGAS 0.4.3 + Azure GPT-4o*
*Timestamp: 2026-02-16 23:30:44*

## The RAGAS Triad Scorecard
| Strategy | Context Recall | Context Precision | Faithfulness | Index Efficiency |
| :--- | :--- | :--- | :--- | :--- |
| **Aegis (Geometric)** | 0.182 | 0.182 | 0.731 | 0.99x |
| **Recursive (Text)** | 0.273 | 0.364 | 0.656 | 0.82x |
| **Naive (Fixed)** | 0.182 | 0.273 | 0.792 | 1.00x |

> [!NOTE]
> **Index Efficiency**: Measures how much redundant text (overlap) is stored. 1.0x means zero redundancy (Aegis). Recursive splitters typically score <0.8x due to 20% overlap bloat.

## Methodology Notice
This benchmark was executed using **Azure OpenAI (GPT-4o)** to ensure high-fidelity semantic auditing. Evaluation used the **RAGAS 0.4.3 Pipeline** for deep semantic auditing of the Aegis Geometric Integrity Protocol vs. traditional text splitting.
