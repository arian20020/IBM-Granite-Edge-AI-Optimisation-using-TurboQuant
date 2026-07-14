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
