# Security, privacy, UX, accessibility and AI quality candidate risks

**Record state:** Candidate backlog — not yet assessed or baselined  
**Owner:** Arian B  
**Parent register:** `../Risk-Register.md`

These rows have been identified for review. Probability, impact, cause, trigger, mitigation, contingency, evidence and residual risk remain pending until the formal risk review.

| Risk ID | Category | Description | Record state | Probability | Impact | Owner | Status |
|---|---|---|---|---|---|---|---|
| R-151 | Security and privacy | A model path or prompt may be inserted unsafely into a command-line command. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-152 | Security and privacy | Spaces, quotation marks or special characters in file paths may create unintended command arguments. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-153 | Security and privacy | A path may point outside the expected model or project directory. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-154 | Security and privacy | Symbolic links or Windows junctions may redirect the application to an unexpected location. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-155 | Security and privacy | A malicious model file may crash the parser or native runtime. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-156 | Security and privacy | A downloaded runtime executable may be changed or replaced. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-157 | Security and privacy | A dependency may send unexpected network traffic. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-158 | Security and privacy | A backend may open a local network port even though the project is meant to avoid one. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-159 | Security and privacy | Prompts or answers may be stored in logs without the user realising. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-160 | Security and privacy | Temporary files may contain private document or prompt content. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-161 | Security and privacy | Local usernames and file paths may appear in screenshots, logs or evidence. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-162 | Security and privacy | API keys, tokens or passwords may be committed to GitHub. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-163 | Security and privacy | Real patient, pupil or confidential information may be used during testing. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-164 | Security and privacy | A cloud dependency may upload model, prompt, document or answer data. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-165 | Security and privacy | The application may claim that local inference guarantees complete privacy. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-166 | Security and privacy | The application may be presented as approved for NHS or school use when it is only a prototype. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-167 | Security and privacy | Healthcare users may treat generated text as medical advice. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-168 | Security and privacy | Educational users may rely on incorrect generated content. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-169 | Security and privacy | A document used by TurboVec may remain inside an index after the original file is removed. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-170 | User experience and accessibility | Users may not understand terms such as GGUF, OpenVINO, KV cache, quantisation or backend. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-171 | User experience and accessibility | Users may not understand the difference between model weight quantisation and KV-cache compression. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-172 | User experience and accessibility | Users may think an Experimental option is fully dependable. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-173 | User experience and accessibility | Automatic or Recommended may be understood as a guarantee that the model will work. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-174 | User experience and accessibility | Error messages may contain raw technical exceptions. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-175 | User experience and accessibility | Error messages may not explain what the user should do next. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-176 | User experience and accessibility | Too many warnings may make the interface confusing. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-177 | User experience and accessibility | A long task may appear frozen because progress is unclear. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-178 | User experience and accessibility | The interface may not work fully using only the keyboard. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-179 | User experience and accessibility | Text or controls may be cut off at 200% Windows text scaling. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-180 | User experience and accessibility | The application may not fit properly on smaller laptop screens. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-181 | User experience and accessibility | Important actual-device or fallback information may be hidden in technical details. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-182 | User experience and accessibility | Users may close the application during a model operation because they do not understand its current state. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-183 | User experience and accessibility | Healthcare and education test tasks may not represent realistic user needs. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-184 | User experience and accessibility | Usability testing may involve too few users to support strong conclusions. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-185 | AI answer quality | Granite may produce incorrect or invented information. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-186 | AI answer quality | Granite may not follow instructions correctly. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-187 | AI answer quality | The model may produce repeated, broken or empty answers. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-188 | AI answer quality | Quantisation may reduce answer quality. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-189 | AI answer quality | TurboQuant may reduce answer quality at longer contexts. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-190 | AI answer quality | OpenVINO conversion may change model behaviour. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-191 | AI answer quality | TurboVec retrieval may provide irrelevant information to the model. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-192 | AI answer quality | The model may ignore relevant retrieved text. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-193 | AI answer quality | The quality prompt set may be too small. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-194 | AI answer quality | The quality prompt set may not cover education and healthcare-style tasks. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-195 | AI answer quality | The quality reviewer may know which route produced each answer and become biased. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-196 | AI answer quality | Different sampling settings may make quality comparisons unfair. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-197 | AI answer quality | A single average quality score may hide serious individual failures. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-198 | AI answer quality | A model that simply produces text may be described as having passed quality testing. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-199 | AI answer quality | Published quality claims may be repeated without testing them on the chosen Granite configuration. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
