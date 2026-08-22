# Inter font assets

Model Inspection uses the Regular (400) and Bold (700) static TrueType faces
from the official Inter 4.1 release. The application packages only those two
font payloads and the complete SIL Open Font License 1.1 text needed for this
design.

- Source repository: `https://github.com/rsms/inter`
- Immutable release archive: `https://github.com/rsms/inter/releases/download/v4.1/Inter-4.1.zip`
- Release archive SHA-256: `9883fdd4a49d4fb66bd8177ba6625ef9a64aa45899767dde3d36aa425756b11e`
- Extracted archive paths: `extras/ttf/Inter-Regular.ttf`,
  `extras/ttf/Inter-Bold.ttf`, and root `LICENSE.txt` (committed as `OFL.txt`)

`inter-manifest.json` records the exact committed byte lengths and lowercase
SHA-256 values. Do not substitute a system font, a variable face, another
weight, or a mutable runtime download.
