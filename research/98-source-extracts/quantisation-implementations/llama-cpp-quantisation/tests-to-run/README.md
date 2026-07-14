---
title: "Tests to run"
status: "full-source-extract"
version: "1.0"
last_updated: "2026-07-14"
source_documents:
  - "Quantisation implementations/llama.cpp quantisation/Tests to run.docx"
verification_note: "Direct Markdown extraction of the supplied DOCX. Formatting may differ, so the original DOCX is preserved in the controlled provenance ZIP."
---

### Tests we will run ourselves

Every repository should go through the same initial test sequence.

1\. Clone the correct branch  
2. Record the exact commit  
3. Build successfully  
4. Run llama-bench or a simple command  
5. Load Granite GGUF  
6. Run F16 KV baseline  
7. Run Q8_0 KV baseline  
8. Run repository-specific TurboQuant mode  
9. Compare output  
10. Measure RAM/VRAM  
11. Test 4K context  
12. Test 8K context  
13. Test 16K context  
14. Test repeated generation  
15. Record crashes and unsupported features
