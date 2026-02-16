import os
import sys
import math
import logging
import re
import time
from datetime import datetime
from typing import List, Dict, Any
import json
import numpy as np
import asyncio
import inspect
import pdfplumber
from openai import AsyncOpenAI
from datasets import Dataset
from langchain_openai import ChatOpenAI
from langchain.schema import Document
from langchain_text_splitters import RecursiveCharacterTextSplitter
from ragas.llms import llm_factory
from ragas.embeddings.base import embedding_factory
from ragas.metrics.collections import (
    ContextRecall, ContextPrecision, Faithfulness, AnswerRelevancy
)

# Ensure local aegis_integrity wrapper is available
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "../../src/python")))
from aegis_integrity.aegis_integrity import (
    GeometricAtom, BoundingBox, GeometricManifest, IntegrityPipe, GridLawDetector
)

def extract_atoms_from_pdf(path: str) -> List[GeometricAtom]:
    atoms = []
    index = 0
    with pdfplumber.open(path) as pdf:
        for page_num, page in enumerate(pdf.pages, 1):
            words = page.extract_words()
            for w in words:
                x = w['x0']
                y = w['top']
                width = w['x1'] - w['x0']
                height = w['bottom'] - w['top']
                text = w['text']
                token_count = max(1, math.ceil(len(text) / 4.0))
                atoms.append(GeometricAtom(text, BoundingBox(x, y, width, height), page_num, token_count, index))
                index += 1
    return atoms

async def run_ragas_benchmark():
    from dotenv import load_dotenv
    from langchain_openai import AzureChatOpenAI
    
    # 1. Setup Azure OpenAI via environment variables
    load_dotenv()
    
    azure_endpoint = os.getenv("AZURE_OPENAI_ENDPOINT")
    azure_api_key = os.getenv("AZURE_OPENAI_API_KEY")
    azure_deployment = os.getenv("AZURE_OPENAI_DEPLOYMENT_NAME", "gpt-4o")
    azure_api_version = os.getenv("AZURE_OPENAI_API_VERSION", "2024-02-15-preview")
    azure_embed_deployment = os.getenv("AZURE_OPENAI_EMBEDDING_DEPLOYMENT", "text-embedding-3-small")

    if not azure_api_key or "your-api-key" in azure_api_key:
        raise ValueError("Azure OpenAI API Key not configured. Please set AZURE_OPENAI_API_KEY in .env")

    print(f"[Azure] Integrating Enterprise Judge ({azure_deployment})...")
    from openai import AsyncAzureOpenAI
    from langchain_openai import AzureOpenAIEmbeddings
    from ragas.embeddings import LangchainEmbeddingsWrapper
    
    azure_client = AsyncAzureOpenAI(
        api_key=azure_api_key,
        azure_endpoint=azure_endpoint,
        api_version=azure_api_version
    )
    
    # In Ragas 0.4.3+, we pass the client to llm_factory
    llm = llm_factory(model=azure_deployment, client=azure_client)
    
    llm_gen = AzureChatOpenAI(
        azure_deployment=azure_deployment,
        azure_endpoint=azure_endpoint,
        api_key=azure_api_key,
        api_version=azure_api_version,
        temperature=0
    )

    # Use Azure OpenAI Embeddings via Modern Interface
    embeddings = embedding_factory(
        model=azure_embed_deployment,
        client=azure_client,
        interface="modern"
    )

    # Initialize Modern Metrics
    metric_objs = {
        "Context Recall": ContextRecall(llm=llm),
        "Context Precision": ContextPrecision(llm=llm),
        "Faithfulness": Faithfulness(llm=llm),
        "Answer Relevancy": AnswerRelevancy(llm=llm, embeddings=embeddings)
    }

    # 2. Setup Data Path
    script_dir = os.path.dirname(os.path.abspath(__file__))
    workspace_root = os.path.abspath(os.path.join(script_dir, "../../"))
    pdf_path = os.path.join(workspace_root, "samples/random_input/technical_paper.pdf")
    
    if not os.path.exists(pdf_path):
        print(f"Error: {pdf_path} not found.")
        return

    # SILENCE PDF WARNINGS
    logging.getLogger("pdfminer").setLevel(logging.ERROR)

    # 3. Ingestion Phase
    print("[Ingestion] Processing Technical Paper...")
    atoms = extract_atoms_from_pdf(pdf_path)
    detector = GridLawDetector()
    structures = detector.detect_table_zones(atoms)
    manifest = GeometricManifest(atoms, structures)
    
    # Aegis Strategy (Geometric) - Sovereign Zero-Overlap
    pipe = IntegrityPipe(manifest, overlap_tokens=0) 
    aegis_chunks = [c.content for c in pipe.generate_chunks(target_tokens=250)]
    
    # 4. Preparation for Evaluation
    dataset_rows = [
        {"question": "What is the primary contribution of the paper?", "ground_truth": "The primary contribution is the introduction of the TraceMonkey JIT compiler for Firefox."},
        {"question": "How does TraceMonkey handle type-specialized code?", "ground_truth": "It uses a trace cache to store and reuse type-specialized machine code sequences."},
        {"question": "What performance increase does TraceMonkey provide for SunSpider?", "ground_truth": "It provides a 2x-4x speedup over the existing interpreter."},
        {"question": "How are recursion and nested loops handled?", "ground_truth": "The trace-based approach naturally handles nesting, while recursion triggers a return to the interpreter."},
        {"question": "What is 'tree grafting' in the context of the paper?", "ground_truth": "Tree grafting is the process of connecting multiple traces into a stable trace tree for complex control flow."},
        {"question": "Which benchmarks were used specifically?", "ground_truth": "SunSpider and bit-fiddling JavaScript benchmarks were used."},
        {"question": "What is the overhead of trace recording?", "ground_truth": "Recording overhead is minimal as it happens during interpretation normally."},
        {"question": "How does the system handle side exits from a trace?", "ground_truth": "Side exits trigger a transition to another trace or return to the interpreter if no trace exists."},
        {"question": "Define the 'Blacklisting' policy mentioned.", "ground_truth": "Paths that fail to form stable traces frequently are blacklisted to avoid re-recording overhead."},
        {"question": "Who are the lead researchers named in the work?", "ground_truth": "Andreas Gal and Brendan Eich are among the lead researchers mentioned."},
        # Aegis Structural Stress Test (High-Fidelity)
        {"question": "Identify all authors and their respective affiliations exactly as listed in the multi-column header.", "ground_truth": "Andreas Gal (Mozilla), Brendan Eich (Mozilla), Mike Shaver (Mozilla), David Anderson (Mozilla), David Mandelin (Mozilla), Mohammad R. Haghighat (Intel), Blake Kaplan (Mozilla), Graydon Hoare (Mozilla), Boris Zbarsky (Mozilla), Jason Orendorff (Mozilla), Jesse Ruderman (Mozilla), Edwin Smith (Adobe), Rick Reitmaier (Adobe), Michael Bebenita (UC Irvine), Mason Chang (UC Irvine), Michael Franz (UC Irvine)."}
    ]

    # 5. Baseline strategies
    full_text = " ".join([a.text for a in atoms])
    naive_chunks = [full_text[i:i+1000] for i in range(0, len(full_text), 1000)]
    splitter = RecursiveCharacterTextSplitter(chunk_size=1000, chunk_overlap=200)
    recursive_chunks = splitter.split_text(full_text)

    strategies = {
        "Aegis (Geometric)": aegis_chunks,
        "Recursive (Text)": recursive_chunks,
        "Naive (Fixed)": naive_chunks
    }

    final_results = []
    audit_data = []

    # 6. Evaluate Strategies
    print("[1/2] Generating RAG Answers (Sequential for Reliability)...")
    
    for name, chunks in strategies.items():
        eval_data = []
        print(f"  Processing {name}...")
        
        # Calculate Index Efficiency (GIP 2.4)
        total_chars = sum(len(c) for c in chunks)
        raw_text_chars = len(full_text)
        efficiency = (raw_text_chars / total_chars) if total_chars > 0 else 0
        
        for i, row in enumerate(dataset_rows):
            question = row['question']
            ground_truth = row['ground_truth']
            print(f"    [{i+1}/{len(dataset_rows)}] Generating answer...")
            
            # Simple Keyword Ranking Retrieval
            q_words = set(question.lower().split())
            chunk_scores = []
            for chunk in chunks:
                c_words = set(chunk.lower().split())
                sim = len(q_words.intersection(c_words)) / len(q_words.union(c_words)) if q_words else 0
                chunk_scores.append((sim, chunk))
            
            chunk_scores.sort(key=lambda x: x[0], reverse=True)
            top_contexts = [c for s, c in chunk_scores[:2]]
            
            # Generate Answer
            from langchain_core.messages import HumanMessage
            context_str = "\n".join(top_contexts)
            prompt = f"Using the context below, answer the question: {question}\n\nContext: {context_str}"
            
            try:
                answer_obj = await asyncio.to_thread(llm_gen.invoke, [HumanMessage(content=prompt)])
                answer = answer_obj.content
            except Exception as e:
                print(f"      !!! Generation failed for {name}: {e}")
                answer = "Error generating answer."

            eval_data.append({
                "user_input": question,
                "response": answer,
                "retrieved_contexts": top_contexts,
                "reference": ground_truth
            })
            
            audit_data.append({
                "Strategy": name,
                "Question": question,
                "Retrieved Contexts": top_contexts,
                "Answer": answer
            })

        print(f"[2/2] Running RAGAS Evaluation for {name} (Sequential Metrics)...")
        res_dict = {"Strategy": name, "Index Efficiency": efficiency}
        
        for m_name, metric in metric_objs.items():
            print(f"    Evaluating {m_name}...")
            sig = inspect.signature(metric.ascore)
            pnames = list(sig.parameters.keys())
            
            scores = []
            for j, row in enumerate(eval_data):
                print(f"      [{j+1}/{len(eval_data)}] Scoring sample...")
                # Truncate context for LLM heavy metrics to avoid context window issues
                row_copy = row.copy()
                if m_name in ["Faithfulness", "Answer Relevancy"] and "retrieved_contexts" in row_copy:
                     row_copy["retrieved_contexts"] = [c[:1500] for c in row_copy["retrieved_contexts"]]
                
                filtered = {k: v for k, v in row_copy.items() if k in pnames}
                try:
                    res = await metric.ascore(**filtered)
                    scores.append(res.value)
                except Exception as e:
                    print(f"      !!! Scoring failed: {e}")
                    scores.append(0.0)
            
            res_dict[m_name] = scores
            
        final_results.append(res_dict)
        # Generate intermediate report to show progress
        generate_reports(final_results, audit_data)

    print("\n[Success] RAGAS Benchmark Complete (Azure GPT-4o).")

def generate_reports(final_results, audit_data):
    timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    
    report = f"# Aegis GIP: High-Fidelity RAG Audit\n"
    report += f"*Evaluation Methodology: Curated Golden Set + RAGAS 0.4.3 + Azure GPT-4o*\n"
    report += f"*Timestamp: {timestamp}*\n\n"
    
    report += "## The RAGAS Triad Scorecard\n"
    report += "| Strategy | Context Recall | Context Precision | Faithfulness | Index Efficiency |\n"
    report += "| :--- | :--- | :--- | :--- | :--- |\n"
    
    for res in final_results:
        def avg(val):
            if isinstance(val, list):
                valid_vals = [x for x in val if x is not None and not np.isnan(x)]
                return sum(valid_vals) / len(val) if val else 0.0
            return val
 
        c_recall = avg(res.get('Context Recall', 0))
        c_precision = avg(res.get('Context Precision', 0))
        faith = avg(res.get('Faithfulness', 0))
        eff = res.get('Index Efficiency', 0)
        
        report += f"| **{res['Strategy']}** | {c_recall:.3f} | {c_precision:.3f} | {faith:.3f} | {eff:.2f}x |\n"
    
    report += "\n> [!NOTE]\n"
    report += "> **Index Efficiency**: Measures how much redundant text (overlap) is stored. 1.0x means zero redundancy (Aegis). Recursive splitters typically score <0.8x due to 20% overlap bloat.\n"
    
    report += "\n## Methodology Notice\n"
    report += "This benchmark was executed using **Azure OpenAI (GPT-4o)** to ensure high-fidelity semantic auditing. Evaluation used the **RAGAS 0.4.3 Pipeline** for deep semantic auditing of the Aegis Geometric Integrity Protocol vs. traditional text splitting.\n"

    with open("INTEGRITY_REPORT.md", "w") as f:
        f.write(report)

    # Audit Log
    audit_report = "# RAG Traceability Audit: Aegis GIP\n\n"
    for item in audit_data:
        audit_report += f"### Strategy: {item['Strategy']}\n"
        audit_report += f"**Question**: {item['Question']}\n"
        audit_report += f"**Generated Answer**: {item['Answer']}\n\n"
        for i, ctx in enumerate(item['Retrieved Contexts']):
            clean_ctx = ctx[:400].replace('\n', ' ')
            audit_report += f"> **Context {i+1}**: {clean_ctx}...\n\n"
        audit_report += "---\n\n"
    
    with open("RAG_AUDIT_LOG.md", "w") as f:
        f.write(audit_report)

if __name__ == "__main__":
    asyncio.run(run_ragas_benchmark())
