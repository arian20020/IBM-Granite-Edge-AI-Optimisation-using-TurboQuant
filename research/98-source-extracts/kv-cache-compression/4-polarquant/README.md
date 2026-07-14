---
title: "4. PolarQuant"
status: "full-source-extract"
version: "1.0"
last_updated: "2026-07-14"
source_documents:
  - "KV-Cache Compression/4. PolarQuant.docx"
verification_note: "Direct Markdown extraction of the supplied DOCX. Formatting may differ, so the original DOCX is also preserved."
---

# 5. PolarQuant

## 5.1 Overview

PolarQuant first randomly rotates the KV vector so that its numerical distribution becomes more predictable and easier to compress. It then converts the rotated coordinates into a collection of radii and angles. The angles are quantised by replacing each precise angle with a nearby value from a small low-bit codebook. During reconstruction, the stored radii and compressed angles are used to rebuild an approximate version of the rotated vector, after which the rotation is reversed to obtain an approximation of the original KV vector.

<img src="assets/82ca99f3df33865757065d323a5c9b97f6fc15af.png" style="width:6.26042in;height:5.92708in" />

## 5.2 Random Preconditioning

Before quantisation, PolarQuant applies random preconditioning to each KV-cache vector. In the practical implementation, this involves multiplying the vector by a shared random rotation matrix. The purpose of this stage is not to compress the vector directly. Instead, it rearranges the vector’s coordinate values into a more regular and predictable representation that is easier to transform into polar coordinates and quantise accurately.

Random preconditioning is therefore a preparation stage rather than a compression stage. The rotation does not itself reduce the number of bits required to store the vector. Its purpose is to improve the statistical structure of the vector so that the later polar angles can be represented accurately using small bit-widths. The PolarQuant paper presents random preconditioning as a central part of the method because it preserves important geometric relationships while making the resulting angle distributions more suitable for quantisation.

### 5.2.1 How Random Rotation Works

A random rotation multiplies a KV-cache vector by a rotation matrix, mixing its coordinate values before the polar transformation and quantisation stages.

For example, consider the vector:

x = \[4, 0\]ᵀ

Its entire magnitude is concentrated in the first coordinate. A 45° rotation matrix can be written as:

R = \[ cos(45°) −sin(45°) \]  
  \[ sin(45°)   cos(45°) \]

Since:

cos(45°) = sin(45°) ≈ 0.707

the matrix is approximately:

R = \[ 0.707 −0.707 \]  
  \[ 0.707  0.707 \]

The rotated vector is calculated as:

y = Rx

Therefore:

y = \[ 0.707 −0.707 \] \[ 4 \]  
  \[ 0.707  0.707 \] \[ 0 \]

This produces:

y ≈ \[2.83, 2.83\]ᵀ

The original vector had all its magnitude concentrated in one coordinate:

x = \[4, 0\]ᵀ

After rotation, the same information is distributed across two coordinates:

y ≈ \[2.83, 2.83\]ᵀ

The length of the original vector is:

‖x‖₂ = √(4² + 0²) = 4

The length of the rotated vector is:

‖y‖₂ = √(2.83² + 2.83²) ≈ 4

The coordinate values have changed, but the vector’s overall magnitude has remained unchanged.

In real high-dimensional KV-cache vectors, random rotation mixes information across many coordinates. This generally reduces the effect of information that is heavily concentrated in a small number of coordinates and produces a more statistically regular representation. This is useful because low-bit quantisation may perform poorly when one coordinate is extremely large, most other coordinates are comparatively small, or unpredictable outliers create a wide numerical range.

Random rotation does not guarantee that every vector becomes perfectly balanced. It also does not mean that every polar angle becomes exactly 45°. Instead, it makes the coordinate and angle distributions more statistically predictable across high-dimensional vectors. This predictable structure allows PolarQuant to represent the resulting angles accurately using small bit-widths and optimised quantisation codebooks.

### 5.2.2 Preservation of Vector Norms and Inner Products

The random rotation changes the individual coordinates used to represent the query and key vectors, but an orthogonal rotation preserves their underlying geometric properties.

Let:

- q represent a query vector;

- k represent a key vector;

- R represent the shared rotation matrix.

The inner product between the rotated vectors is:

(Rq)ᵀ(Rk)

This can be rearranged as:

(Rq)ᵀ(Rk) = qᵀRᵀRk

An orthogonal rotation matrix satisfies:

RᵀR = I

where I represents the identity matrix. Therefore:

(Rq)ᵀ(Rk) = qᵀIk

and consequently:

(Rq)ᵀ(Rk) = qᵀk

This means that applying the same orthogonal rotation to the query and key vectors does not change their inner product. Since the attention mechanism uses query-key inner products to measure the relationship between the current query and previous keys, the rotation itself does not damage the attention information.

During PolarQuant dequantisation, the reconstructed vector is multiplied by the transpose of the rotation matrix, Rᵀ, to return it to the original coordinate system. Any difference between the final reconstructed vector and the original vector is therefore introduced by the later angle-quantisation stage rather than by the random rotation itself. The paper’s practical implementation uses a square random rotation matrix satisfying RᵀR = I, which preserves norms and inner products exactly before quantisation.

### 5.2.3 Meaning of “Randomising the Distribution”

Randomising the distribution does not mean scrambling, deleting or replacing the information contained in the vector. Instead, the random rotation mixes the coordinate values so that the information is less likely to remain concentrated in a few unusually large coordinates.

For example, consider the vector:

x = \[12, 0.1, −0.2, 0.05\]

This vector contains a large outlier in its first coordinate. After multiplication by a suitable random rotation matrix, the same information may be spread more evenly across several coordinates. The exact transformed values depend on the selected rotation matrix, but the vector’s length and geometric relationships are preserved before quantisation.

This produces a more regular statistical distribution. When the rotated vector is subsequently transformed into polar coordinates, the resulting angle distributions become more predictable. In particular, the angles generated at later levels of the recursive polar transformation become increasingly concentrated around:

π/4 = 45°

This does not mean that every angle is exactly 45°. It means that many later-level angles are likely to occur within a narrower region around 45°, rather than being spread unpredictably across the entire possible range.

Because these angle distributions can be mathematically derived, PolarQuant can construct optimised low-bit codebooks that place more representative values in the regions where angles are most likely to occur. This reduces quantisation error and avoids the need to calculate and store separate normalisation constants, such as scales and zero points, for every small block. The paper explains that later-level angles become increasingly concentrated around π/4 and can therefore be quantised independently using codebooks designed to minimise mean-squared error.

### 5.2.4 Theoretical Analysis and Practical Implementation

The PolarQuant paper uses two closely related forms of random preconditioning.

In the theoretical analysis, the paper considers a random Gaussian projection matrix S whose entries are independently sampled from a normal distribution:

Sᵢⱼ ~ N(0, 1)

For an original vector x ∈ ℝᵈ, applying this matrix gives:

Sx ~ N(0, ‖x‖₂²Iₘ)

This means that the transformed coordinates follow a predictable multivariate Gaussian distribution. The Gaussian formulation allows the researchers to mathematically derive the distributions of the resulting radii and polar angles.

In the practical implementation, however, PolarQuant uses a square random rotation matrix satisfying:

SᵀS = I

This practical rotation preserves vector norms and inner products exactly before quantisation. However, the paper notes that it does not maintain all the strict coordinate-independence assumptions used in the theoretical Gaussian-projection analysis.

The two matrix types therefore serve the same broad purpose but should not be treated as identical. The Gaussian projection supports the theoretical proof, while the orthogonal rotation provides a reversible and geometry-preserving transformation for practical implementation.

### 5.2.5 Use of a Shared Rotation Matrix

PolarQuant does not generate a different random rotation matrix for every token or vector. In the practical implementation, one generated rotation matrix is shared across:

- key embeddings;

- value embeddings;

- attention layers;

- attention heads.

Using the same matrix consistently ensures that transformed vectors remain geometrically compatible. It also avoids the need to store a separate matrix for every token, layer or attention head.

During dequantisation, the transpose of the shared matrix, Sᵀ, is applied to the reconstructed vector to reverse the original rotation and return the approximation to the original coordinate system. Algorithm 1 of the PolarQuant paper shows this final inverse transformation in the dequantisation procedure.

### 5.2.6 Contribution to Efficient Quantisation

Traditional KV-cache quantisation methods commonly divide vectors into small blocks and normalise each block independently. This normally requires the calculation and storage of additional full-precision values, including:

- minimum and maximum values;

- scales;

- zero points.

These quantisation constants create additional memory and processing overhead.

PolarQuant instead applies random preconditioning so that the transformed coordinate and angle distributions become predictable. The resulting polar angles can then be compressed using optimised shared or precomputed codebooks. This removes the need to calculate and store separate scale and zero-point values for every small data block.

However, PolarQuant does not eliminate all supporting information. Its compressed representation still requires:

- radius information;

- quantised angle indices;

- angle codebooks;

- the shared rotation matrix.

Therefore, the correct claim is that PolarQuant removes traditional per-block normalisation metadata rather than eliminating all quantisation overhead.

### 5.2.7 Experimental Evidence and Limitations

Figure 2 of the PolarQuant paper compares polar-angle distributions with and without random preconditioning. The results show that preconditioning reduces problematic outliers at the first polar level and produces more regular angle distributions. At the later recursive levels, the distributions become increasingly concentrated around π/4, making the angles easier to represent using low-bit codebooks.

This provides experimental evidence that random preconditioning improves the statistical structure of the vectors before quantisation. However, this should not be interpreted as guaranteeing a large final accuracy improvement for every task, model or hardware platform. The effect on end-to-end model quality may vary depending on the benchmark, model architecture, context length and implementation.

The experiments in the PolarQuant paper were performed using NVIDIA GPU hardware and models such as Llama-3.1-8B-Instruct. Therefore, the same performance improvements cannot automatically be assumed for IBM Granite models, Intel CPUs, integrated GPUs, NPUs or OpenVINO. These environments require separate experimental validation.

### 5.2.8 Process Summary

The role of random preconditioning in PolarQuant can be summarised as follows:

Original KV vector  
→ multiplication by a shared random rotation matrix  
→ coordinate information becomes more evenly mixed  
→ vector norms and inner products are preserved  
→ recursive polar transformation  
→ predictable radii and angle distributions  
→ low-bit angle quantisation using optimised codebooks  
→ approximate vector reconstruction  
→ inverse rotation using Sᵀ

Random preconditioning is therefore an important preparation stage within PolarQuant. It does not directly compress the KV vector. Instead, it transforms the vector into a predictable and quantisation-friendly representation, allowing the later polar-angle compression stage to use fewer bits while maintaining low reconstruction error.

## 5.3 Recursive Polar Transformation

The general idea is:

Take two numbers, replace them with one radius and one angle, and repeat the same process on the new radii.

<img src="assets/bd4150b538c2c0e6a8261d0e75b7165267e6a60e.png" style="width:6.26042in;height:2.64583in" />5.3.1 Step 1: Divide the vector into pairs

In the first step of figure 1, we have a vector: \[x1 ,x2 ,x3 ,x4 ,…,xd−1 ,xd \]

We divide it as:

(x1 ,x2 ), \[Equation\]\[Equation\]\[Equation\](xd−1 ,xd )

### 5.3.2 Step 2: Convert Every Pair into a Radius and Angle (Levell 1)

So for each pair of coordinates, we generate a Level 1 radius and a level 1 angle.

#### *5.3.2.1 Level 1 radius*

<img src="assets/89f26a4be3c45c42c800f36fc27a808feafb1953.png" style="width:5.77083in;height:3.57292in" />

#### *5.3.2.2 Level 1 angle*

For the angle, we use the tan inverse.

So for the first pair (x1, x2), we do:

<img src="assets/5e0980820615d08ddd3820b8e092099b63653363.png" style="width:1.75in;height:0.65625in" />

So, for example, we could have (x1, x2) = (3,4).

<img src="assets/de151b71b266ed4177453a0a78befd5b4b3b0954.png" style="width:4.67708in;height:3.63542in" />

#### *5.3.2.3 What does level 1 output?*

It produces 2 vectors: an angle vector and a radius vector:

Angle vector:

<img src="assets/977709f90c95a8937c79327426ec3581a861400e.png" style="width:2.42708in;height:0.5in" />

Radius vector:

<img src="assets/7f5d51f13bafa7d639dcd99009f116695d706a48.png" style="width:2.28125in;height:0.52083in" />

There are d/2 angles and d/2 radii because we split the vector into pairs of coordinates.

So:

<img src="assets/35abcfac6d114f118833d9a554368b265baed080.png" style="width:1.03125in;height:0.5in" /> and <img src="assets/9381b76465eda92e03540c6f5c23140dfb44e9bf.png" style="width:1.04167in;height:0.47917in" />

#### *5.3.2.4 What info does a level 1 represent?*

The radius says how much total magnitude the pair contains;

the angle says how that magnitude is divided between the two coordinates.

So if we have an angle and radius, then we can reconstruct it using the cosine:

<img src="assets/83be48b62646838976abc46876a632e09703f96d.png" style="width:6.26042in;height:2.67708in" />

<img src="assets/e267e7c0eacd4020ebd7c4324c2060d36e51bb5b.png" style="width:6.26042in;height:1.58333in" />

### 5.3.3 Step 3: Pair the Level 1 radii

This is where the recursive part begins.

So for the radii vector generated from level 1 , we pair them just like how we did for the coordinates:

<img src="assets/c8e0b2afe09bac03ba24c2da6398ba1c15868ee9.png" style="width:1in;height:1.42708in" />

### 5.3.4 Step 4: Produce a Level 2 radius and angle

<img src="assets/eb639d7550382e27d01da17980f19da2929ae212.png" style="width:4.65625in;height:3.13542in" />

#### *5.3.4.1 What does this level 2 radius mean?*

<img src="assets/a903721455acd2330cf70f3b2c245e28672b7e38.png" style="width:4.52083in;height:1.78125in" />

#### *5.3.4.2 What does the Level 2 angle mean?*

<img src="assets/03d89f09bb264f391635311278157f2f3fc5ae02.png" style="width:5.69792in;height:3.8125in" />

#### *5.3.4.3 What is the output of Level 2?*

Level 1 contained d/2 radii.

Level 2 contains d/4 radii and d/4 new angles.

Therefore:

<img src="assets/378ea807f6159f401eb64fcaa8c21373440e057d.png" style="width:1.15625in;height:1.25in" />

### 5.3.5 So what is the general recursive formula?

<img src="assets/17b79e4d3599bf99b0b38de9b5c4a7360e276791.png" style="width:6.26042in;height:5.02083in" />5.3.6 How do we determine the number of levels?

<img src="assets/a580273cde34c697247a6a058c2e4fea8cc481c8.png" style="width:6.03125in;height:6.26042in" />

### 5.3.7 Last step: One overall radius remains

<img src="assets/0007ac38a1c3af23b4b328dda3810e5813e0c8c8.png" style="width:6.26042in;height:4.34375in" />5.3.8 How many angles are produced?

<img src="assets/4e78dd37e5a93d37820c2ff5aa615a52ed1be77a.png" style="width:4.45833in;height:2.79167in" />

### 5.3.9 So does polar transformation compress the vector itself?

No, it just replaces the original d cartesian coordinates with 1 radius + d-1 angles.

Memory reduction starts from the next stage, when angles are stored using very few bits.

### 5.3.10 Tree like visualisation of polar transformation of an 8D vector

overall radius

│

one level-3 angle

/ \\

magnitude x₁:₄ magnitude x₅:₈

│ │

level-2 angle level-2 angle

/ \\ / \\

x₁:₂ x₃:₄ x₅:₆ x₇:₈

│ │ │ │

level-1 level-1 level-1 level-1

angle angle angle angle

### 5.3.11 Direct formula from definition 1

<img src="assets/2c921fc38ad7f928557f3a5d6337d87f59b49b3f.png" style="width:6.26042in;height:1.9375in" />5.3.12 Intuitive version of the Formula

<img src="assets/2e68dd5ce1e78ff48142687b0fc3b8395078a9d6.png" style="width:2.40625in;height:2.0625in" />

## 5.4 Angle Quantisation

Polar transformation generates 1 angle and d-1 precise angles. PolarQuant compresses these angles by replacing each precise angle with the closest value from a small set of allowed angles, then stores only the small code identifying that replacement.

### 5.4.1 Why Compress the Angles?

<img src="assets/79c75b25b9e8d65b6e6831fd70cdc6400a6f3b04.png" style="width:5.30208in;height:6.26042in" />

### 5.4.2 Bit Width

<img src="assets/941387c46a04962c4f3691419debcfa80477de9b.png" style="width:6.26042in;height:5.41667in" />5.4.3 What even is the angle codebook?

<img src="assets/9f9057cf90bde4c1fc62b210b07c087714733f78.png" style="width:6.26042in;height:5.3125in" />Each recursive level has its own codebook, since the angles intervals are slashed in each iteration, so using the same codebook for every level would be inefficient.

### 5.4.4 How does PolarQuant choose the best codebook values?

It tries to minimised the average squared difference between the original exact angle and the chosen replacement angle.

<img src="assets/ffe4d8d97d26faa35fb241cc83bfa595265a42b9.png" style="width:1.58333in;height:0.77083in" />

So, the goal is to position the available replacement angles so that, on average, they remain as close as possible to the real angles.

### 5.4.5 How the actual quantisaation process works

<img src="assets/dfad6f5135a4e20ab131880e92b411629a91cbfd.png" style="width:6.26042in;height:1.57292in" /><img src="assets/e95c3512983ed0cccb62f25d4eef5610ba8f2623.png" style="width:6.26042in;height:5.58333in" />

### 5.4.6 So now what is actually stored?

After quantisation, PolarQuant stores:

The remaining radius

The low bit indices of the selected angles

The codebook values needed for reconstruction

### 5.4.7 So what is the output for the full transformation?

<img src="assets/4787612463f0cce394abfd15287f643d3ced9012.png" style="width:6.26042in;height:1.91667in" />

The letter \[Equation\] therefore represents the quantised angle codes, not the original floating-point angles.

## 5.5 Vector reconstruction (dequantisation)

So we want to: use the retained radius values and the quantised angle codes to rebuild an approximate version of the rotated KV vector, then reverse the original random rotation to return to the original coordinate system.

### 5.5.1 Reconstruction works backwards through the polar tree

<img src="assets/ea345ed2be0b19fd0a324f0582d1b62a28556789.png" style="width:6.26042in;height:2.47917in" />So we are trying to get back to level 1.

### 5.5.2 Look upthe quantised angle

So we read the stored low-bit code and use it to find the approximate angle selected during quantisation.

<img src="assets/df8416c421589e3853a62872e7c390871609eb7c.png" style="width:6.26042in;height:3.94792in" />5.5.3 Splitting one parent radius into two child values

<img src="assets/ab5a92110606d9301470d4d5ef88fac47ff842a2.png" style="width:6.26042in;height:4.39583in" />example

Suppose:

\[Equation\]

and the reconstructed angle is:

\[Equation\]

The first child becomes:

\[Equation\]\[Equation\]\[Equation\]

The second child becomes:

\[Equation\]\[Equation\]\[Equation\]

Therefore:

\[Equation\]\[Equation\]\[Equation\]\[Equation\]\[Equation\]

reconstructs the approximate coordinate pair:

\[Equation\]\[Equation\]

If the original exact angle was \[Equation\], the original pair was:

\[Equation\]\[Equation\]

The reconstruction is close but not identical because the precise angle was replaced with a quantised approximation.

### 5.5.4 Reconstruction across several levels

<img src="assets/5cac7d5066c26142eb8c6ebd704b56bbb0c98caa.png" style="width:5.1875in;height:6.26042in" />

### 5.5.5 Reverse the random preconditioning rotation

At this point PolarQuant has reconstructed the approximation of the rotated vector.

<img src="assets/bbeb898452f54b13af5f24462794fd9791e5528c.png" style="width:6.17708in;height:5.40625in" />

### 5.5.6 The complete reconstruction flow

<img src="assets/822da8c0560081a67d19ce59689fd5c75ddbc49a.png" style="width:6.26042in;height:6.11458in" />We will be using the reconstructed vectors for applying attention and its formula. And we expect that query-key attention scores remain close, the weighted value output remains close, the models final answers remain useful.

<img src="assets/347ce3813f210524012411828b469e3dbbcedc65.png" style="width:5.92708in;height:6.26042in" />
