# Version 3 revision report

## User-reported problems

1. Markdown files displayed `Error in user YAML` because source IDs had been inserted under scalar YAML fields.
2. The curated documents were too short compared with the supplied research.
3. The main files did not retain enough of the original step-by-step explanation.

## Corrections

- Removed YAML front matter from all Markdown files.
- Rebuilt the curated documents from the full source extracts.
- Retained the original wording, numerical examples, equations and learning order.
- Removed only exact repeated prose and formatting noise.
- Added visible overview, step-by-step, project-relevance and summary sections.
- Preserved all original DOCX and PNG files.
- Added detailed application and testing guides for areas where the supplied source document was blank or only a checklist.

## Validation targets

- no Markdown file begins with YAML front matter;
- all 44 original DOCX files remain present;
- all 56 PNG files remain present;
- every source-register extract path exists;
- every curated destination exists;
- local Markdown links resolve;
- main curated word count is substantially closer to the original research volume.
