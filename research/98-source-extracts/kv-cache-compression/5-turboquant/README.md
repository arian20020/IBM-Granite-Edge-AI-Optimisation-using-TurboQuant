# 1. Introduction

TurboQuant is a novel vector-compression algorithm designed to reduce KV-cache memory and bandwidth bottlenecks. In the researchers’ experiments, it achieved substantial KV-cache compression while maintaining model accuracy and introducing negligible runtime overhead. These results suggest that TurboQuant could represent an important advance in efficient local LLM inference, although its performance on IBM Granite models and Intel AI PCs still requires experimental validation.

# 2. The basic idea

TurboQuant stores an approximate version of each KV-cache vector using very few bits, then stores a tiny extra clue about the information lost during compression. This lets the model estimate almost the same attention scores without keeping every original decimal number.

# 3. The problem

The KV cache stores the previous **key and value vectors,** so the model does not have to calculate them again every time it generates a new token.

That saves computation, but it uses a lot of memory because the model stores vectors for:

- every previous token

- every attention layer

- every attention head

As the conversation gets longer, the KV cache grows. Both the QJL and PolarQuant papers identify this growth as a major memory problem for long-context inference.

# 4. TurboQuant’s Two-Stage Compression Process

TurboQuant uses a two-stage process to compress high-dimensional vectors stored in the KV cache. In the first stage, the original vector is randomly rotated and represented using lower-precision values. This stage is designed to minimise mean-squared error, meaning the compressed approximation remains as numerically close as possible to the original vector while requiring fewer bits. Most of the original vector’s information is retained during this stage. The TurboQuant paper describes this as its MSE-optimised quantisation stage in the abstract and Sections 1 and 1.3 on pages 1–5.

The second stage deals with the small amount of error remaining after the first compression. This difference between the original vector and its compressed approximation is known as the residual. TurboQuant applies the 1-bit Quantized Johnson-Lindenstrauss method, or QJL, to this residual. Rather than storing every lost value, QJL stores a compact mathematical sketch that helps correct the inner-product calculations used to produce attention scores. The TurboQuant paper shows this process directly in Algorithm 2 on page 12, where the residual is calculated and passed through QJL. The QJL paper explains the one-bit sketch and its inner-product estimator in the abstract and Sections 1.1 and 3.

Together, the two stages provide a small approximation of the original vector and a lightweight correction for the remaining error. This reduces KV-cache memory and memory-transfer demands while aiming to preserve accurate attention calculations. However, these benefits still need to be tested on IBM Granite models and Intel AI PC hardware.

# 5. TurboQuant_mse

## 5.1 What is it trying to do?

TurboQuant*mse\_{mse}*mse tries to compress the vector while making the reconstructed vector as numerically close as possible to the original.

It is a first-stage quantiser designed to minimise

> **Archived image:** `e7271d51f31631440f88a7337d39f2bec2f7b468.png` is preserved in the controlled provenance ZIP and is not duplicated in Git.


> **Archived image:** `9bcf2fa7a8de2618060da8ad49e06546d7395a9b.png` is preserved in the controlled provenance ZIP and is not duplicated in Git.

TurboQuant*mse\_{mse}* tries to compress the vector while making the reconstructed vector as numerically close as possible to the original.

## 5.2 Complete first-stage flow


> **Archived image:** `0b6508ba98c980cb9d4b2f5be50b6a436dda4e20.png` is preserved in the controlled provenance ZIP and is not duplicated in Git.


## 5.3 Step 1: Normalising the Vector to Unit Length

Before applying the main TurboQuant compression process, the input vector is converted into a unit-length vector. This means that the vector is rescaled so that its total length is equal to 1.

For an input vector:

x = \[x₁, x₂, …, x_d\]

its Euclidean norm is calculated as:

‖x‖₂ = √(x₁² + x₂² + … + x_d²)

The vector is then divided by this norm:

u = x / ‖x‖₂

The resulting vector u has a norm of 1:

‖u‖₂ = 1

For example, consider the vector:

x = \[3, 4\]

Its norm is:

‖x‖₂ = √(3² + 4²)

‖x‖₂ = √25

‖x‖₂ = 5

Each coordinate is then divided by 5:

u = \[3/5, 4/5\]

u = \[0.6, 0.8\]

The new vector has unit length:

‖u‖₂ = √(0.6² + 0.8²)

‖u‖₂ = √(0.36 + 0.64)

‖u‖₂ = 1

This process separates the vector into two types of information:

- its overall magnitude, represented by ‖x‖₂;

- its direction, represented by the unit vector u.

The original vector can therefore be written as:

x = ‖x‖₂u

TurboQuant stores or accounts for the original norm and applies its main compression process to the unit-length direction. After the unit vector has been compressed and approximately reconstructed, the original magnitude is restored by multiplying the reconstructed unit vector by the stored norm:

x̃ = ‖x‖₂ũ

where:

- ũ is the reconstructed approximation of the unit vector;

- x̃ is the final approximation of the original vector.

For example, suppose the compressed unit vector is reconstructed as:

ũ = \[0.58, 0.81\]

The original norm was:

‖x‖₂ = 5

The reconstructed vector is therefore:

x̃ = 5\[0.58, 0.81\]

x̃ = \[2.90, 4.05\]

The original vector was:

x = \[3, 4\]

Therefore, the reconstructed vector remains close to the original, although a small amount of error has been introduced by quantisation.

Converting every vector to unit length makes the later mathematical analysis and quantisation process more predictable because all vectors have the same overall scale. TurboQuant can therefore focus on compressing the direction of the vector without also having to account for large differences in vector magnitude.

This normalisation step is not the main compression stage. Its purpose is to prepare the vector for the later random rotation and coordinate quantisation stages. It should also be distinguished from traditional per-block quantisation normalisation, which may require separate scales and zero points for many small blocks. TurboQuant only needs to account for the overall norm of the vector, which can then be reapplied during reconstruction. The TurboQuant paper uses the unit-norm assumption in its theoretical analysis and explains that vectors with different magnitudes can be handled by storing their norms separately.

## 5.4 Step 2: Random Rotation of the Normalised Vector

After the original vector has been normalised to unit length, TurboQuant applies a random rotation to it.

The normalised vector is:

u = x / \|\|x\|\|\_2

TurboQuant then multiplies this vector by a random rotation matrix:

y = Pi u

where:

u = the normalised input vector  
Pi = the random rotation matrix  
y = the rotated vector

The purpose of the rotation is to mix the coordinates of the vector. This reduces the chance that most of the vector's information is concentrated in only one or two coordinates.

For example, before rotation, a vector may look like:

u = \[1, 0, 0, 0\]

In this vector, all the information is concentrated in the first coordinate.

After random rotation, it may look more like:

y = \[0.48, -0.52, 0.41, 0.57\]

The exact values depend on the random rotation. However, the main idea is that the information has been spread more evenly across the coordinates.

The word "shuffle" can help explain the basic idea, but the coordinates are not simply moved into different positions. The rotation mathematically mixes the coordinates together and creates new coordinate values.

The rotation preserves the overall length of the vector:

\|\|y\|\|\_2 = \|\|u\|\|\_2 = 1

This means that the vector remains unit length after rotation.

The rotated coordinates are not guaranteed to become exactly equal. Instead, the information becomes more balanced in a statistical sense. This makes extremely large coordinates and heavily concentrated information less likely.

This prepares the vector for the next quantisation stage. Because the rotated coordinates now follow a more balanced and predictable pattern, TurboQuant can apply the same optimised scalar quantiser to each coordinate.

### 5.5 Constructing an Optimised Scalar Codebook

After the vector has been normalised and randomly rotated, TurboQuant_mse must decide how each rotated coordinate will be represented using a small number of bits.

Suppose each rotated coordinate is allocated b bits. The number of possible values that can be represented is:

Number of codebook values = 2^b

These representative values are called centroids.

For example:

1 bit gives 2 centroids  
2 bits gives 4 centroids  
3 bits gives 8 centroids  
4 bits gives 16 centroids

The codebook can be written as:

Codebook = \[c_1, c_2, ..., c\_(2^b)\]

Each centroid is a possible replacement value for a rotated coordinate.

TurboQuant chooses the centroid values so that the expected squared difference between the original coordinate and its replacement is as small as possible. This supports the main aim of TurboQuant_mse, which is to minimise the mean squared error between the original vector and the reconstructed vector.

The codebook is created using the Lloyd-Max algorithm. This can also be understood as a one-dimensional continuous k-means problem.

In simple terms, the algorithm places the limited replacement values in the parts of the coordinate distribution where values are most likely to appear. This reduces the amount of error caused when a precise coordinate is replaced by a nearby centroid.

After random rotation, most coordinate values are expected to be relatively close to zero. Therefore, the codebook generally places more useful replacement values around the most likely areas of the distribution, rather than spacing all values equally across the full range from -1 to 1.

The codebooks are calculated in advance for useful combinations of:

d = the vector dimension  
b = the number of bits used per coordinate

The system can then reuse these codebooks for new vectors. It does not need to run the Lloyd-Max optimisation separately for every new key or value vector.

In simple terms:

TurboQuant creates a small list of carefully chosen replacement values. These values are positioned where rotated coordinates are most likely to occur, which helps reduce reconstruction error.

The TurboQuant paper describes this codebook construction in Section 3.1 and around Algorithm 1 on pages 9 and 10. 2504.19874v1.pdf

### 5.6 Quantising Each Rotated Coordinate

After the optimised codebook has been created, TurboQuant quantises each coordinate in the rotated vector separately.

For every rotated coordinate y_j, TurboQuant finds the nearest centroid in the codebook.

This can be written as:

idx_j = the value of k that minimises \|y_j - c_k\|

where:

y_j = the original rotated coordinate  
c_k = one centroid in the codebook  
idx_j = the index of the nearest centroid

In simple terms:

TurboQuant compares the coordinate with every available centroid and chooses the centroid with the closest value.

For example, suppose the codebook is:

Codebook = \[-0.75, -0.20, 0.20, 0.75\]

Suppose one rotated coordinate is:

y_j = 0.16

The nearest centroid is:

0.20

Instead of storing the precise floating-point value 0.16, TurboQuant stores the index that identifies the centroid 0.20.

Because this codebook contains four centroids, only 2 bits are needed to identify one of them.

For example, the indices could be represented as:

00 = -0.75  
01 = -0.20  
10 = 0.20  
11 = 0.75

The exact binary assignment is only an example. The important point is that the system stores a small index instead of a full-precision decimal value.

Consider the following rotated vector:

y = \[0.16, -0.69, 0.31, -0.08\]

Using the example codebook, the nearest centroid approximations may be:

y_tilde = \[0.20, -0.75, 0.20, -0.20\]

TurboQuant does not need to store these four decimal values directly. It only needs to store the four small codebook indices.

The values used in this example are simplified. The real TurboQuant codebook values are calculated from the expected Beta-type coordinate distribution and depend on the vector dimension and chosen bit-width.

In simple terms:

Each precise rotated coordinate is replaced by the nearest value in the codebook. TurboQuant then stores the small codebook index instead of the original floating-point number.

### 5.7 Dequantising and Reconstructing the Vector

When TurboQuant needs to use the compressed vector, it first reconstructs an approximate version of the rotated vector.

For every stored index idx_j, the system retrieves the corresponding centroid from the codebook:

y_tilde_j = c\_(idx_j)

Repeating this process for every coordinate produces the reconstructed rotated vector:

y_tilde = \[y_tilde_1, y_tilde_2, ..., y_tilde_d\]

This reconstructed vector is only an approximation because each original coordinate was replaced by a nearby centroid.

The vector is still represented in the rotated coordinate system. TurboQuant must therefore reverse the original random rotation.

The reconstructed vector is calculated as:

x_tilde = Pi^T y_tilde

where:

Pi = the original random rotation matrix  
Pi^T = the transpose of the rotation matrix  
y_tilde = the reconstructed rotated vector  
x_tilde = the reconstructed approximation of the original vector

Because Pi is an orthogonal rotation matrix:

Pi^T = Pi^(-1)

This means that the transpose of Pi acts as its inverse. Multiplying by Pi^T reverses the original rotation and returns the vector to its original coordinate system.

If the original vector was normalised before compression, its stored norm is also reapplied:

final x_tilde = \|\|x\|\|2 multiplied by x_tilde

The result is the Stage 1 approximation of the original vector.

In simple terms:

TurboQuant uses the stored indices to retrieve approximate coordinate values, rebuilds the rotated vector, and reverses the rotation to produce an approximation of the original vector.

Algorithm 1 in the TurboQuant paper presents the quantisation and dequantisation process. 2504.19874v1.pdf

### 5.8 Similarities Between TurboQuant_mse and PolarQuant

TurboQuant_mse and PolarQuant share several broad ideas.

Both methods:

- begin by applying a random rotation to the vector

- use the rotation to create a more predictable statistical representation

- use optimised codebooks

- replace precise values with low-bit codebook indices

- reconstruct an approximate version of the vector

- reverse the original rotation during reconstruction

- aim to reduce quantisation error while using fewer bits

The main shared idea is that random rotation prepares the vector for more effective low-bit quantisation.

### 5.9 Differences Between TurboQuant_mse and PolarQuant

Although the two methods begin with random rotation, their later processing steps are different.

PolarQuant:

- converts the rotated vector into recursive polar coordinates

- represents the vector using radii and angles

- mainly quantises the angles

- uses angle distributions that depend on the level of the recursive transformation

- reconstructs the vector using repeated sine and cosine calculations

TurboQuant_mse:

- keeps the vector in ordinary Cartesian coordinates after rotation

- directly quantises every rotated coordinate

- uses one predictable coordinate distribution

- applies a scalar quantiser independently to each coordinate

- reconstructs coordinates by retrieving their codebook centroids

- reverses the rotation using Pi^T

Therefore, TurboQuant_mse is not simply PolarQuant with a small modification.

The two methods share the use of random rotation, but they use different representations and different quantisation procedures after the rotation.

In simple terms:

PolarQuant converts the vector into radii and angles before quantisation. TurboQuant_mse keeps the rotated coordinates and quantises them directly.

### 5.10 Why TurboQuant_mse Is Not Enough by Itself

TurboQuant_mse is designed to produce a reconstructed vector that is numerically close to the original vector.

Its objective is to minimise:

\|\|x - x_tilde\|\|2^2

where:

x = the original vector  
x_tilde = the reconstructed vector

A smaller value means that the reconstructed vector is closer to the original vector.

However, a low reconstruction error does not automatically guarantee that query-key inner products will remain unbiased.

The attention mechanism depends on inner products such as:

q dot x

where:

q = the current query vector  
x = a stored key vector

After Stage 1, the model may instead calculate:

q dot x_tilde

Even when x_tilde is close to x, the calculated inner product may be slightly too high or too low.

At very low bit-widths, TurboQuant_mse may systematically shrink or distort these inner-product estimates. This is important because the inner products are used to calculate attention scores before softmax.

Full TurboQuant therefore uses two stages.

Stage 1:

TurboQuant_mse creates a low-error approximation of the original vector.

Stage 2:

QJL records compact information about the remaining reconstruction error.

The remaining error is called the residual:

r = x - x_tilde

The residual represents the information that was lost during Stage 1.

Full TurboQuant normally allocates:

b - 1 bits per coordinate to TurboQuant_mse  
1 additional bit per coordinate to QJL applied to the residual

The QJL sketch helps estimate the effect that the residual would have had on the original query-key inner product.

Therefore:

Stage 1 aims to reconstruct the vector accurately.  
Stage 2 aims to correct the remaining inner-product error.

# 6. QJL

QJL projects each key using a random matrix, compresses the projected key into one-bit signs, keeps the current projected query at higher precision, and combines them with the key’s magnitude to estimate the original query-key attention score. The resulting score is then used to weight the value paired with that key.

So QJL does not try to rebuild the entire key vector. It only tries to preserve the number that attention actually needs: the query–key inner product.

### 6.1 First understand what the key and value does


> **Archived image:** `0e36f2a6ec50981ac53f9bfe5aa36c6abfb009db.png` is preserved in the controlled provenance ZIP and is not duplicated in Git.


> **Archived image:** `edf4a88a05681820238b785d699a82636aecb26d.png` is preserved in the controlled provenance ZIP and is not duplicated in Git.


### 6.2 What Problem is QJL Solving?

Normally, every key contains many precise decimal values:

*k=\[0.83,−1.24,0.17,…\]*

The cache stores a key for every previous token, in every relevant attention layer.

That consumes substantial memory.

Traditional quantisation might replace the precise values with low-bit integers, but it usually also needs:

- a scale

- a zero point

- sometimes separate constants for every block

QJL asks a different question:

Do we really need to preserve every individual number in the key, or do we mainly need enough information to estimate *q⋅k*?

Its answer is:

We can store a very small one-bit sketch of the key and still estimate the inner product.

The solution QJL provides is by converting random projections of the key into one-bit signs, then uses those signs with a precise projected query to estimate the original query–key inner product.

## 6.3 Step 1: Create the Random Matrix

QJL begins by applying a Johnson–Lindenstrauss random projection to each key vector. The purpose of this stage is to create several random measurements of the key before reducing those measurements to one-bit signs.

Let the original key vector be:

k ∈ ℝᵈ

This means that k contains d real-valued coordinates. For example:

k = \[2, −1, 4\]ᵀ

In this example:

d = 3

QJL creates a random matrix:

S ∈ ℝᵐˣᵈ

where:

d is the number of coordinates in the original key vector;

m is the number of random measurements produced by the projection;

S contains m rows and d columns;

each entry in S is randomly sampled from a normal distribution.

For example, if QJL takes two random measurements of a three-dimensional key, then:

m = 2

and:

S ∈ ℝ²ˣ³

An example matrix is:

S = \[ 0.5  1  −0.5 \]  
  \[ 1  −0.25 0.5 \]

The key is multiplied by this matrix:

Sk

Each row of S calculates one dot product with the key vector. Therefore, each row can be understood as examining the key from a different random direction.

For the first row:

s₁ = \[0.5, 1, −0.5\]

The first measurement is:

s₁ · k = (0.5 × 2) + (1 × −1) + (−0.5 × 4)

s₁ · k = 1 − 1 − 2

s₁ · k = −2

For the second row:

s₂ = \[1, −0.25, 0.5\]

The second measurement is:

s₂ · k = (1 × 2) + (−0.25 × −1) + (0.5 × 4)

s₂ · k = 2 + 0.25 + 2

s₂ · k = 4.25

The projected key is therefore:

Sk = \[−2, 4.25\]ᵀ

The original key contained three coordinates, while the projected key contains two random measurements:

k ∈ ℝ³

Sk ∈ ℝ²

The key has not been transformed into a matrix. Instead, the random matrix has transformed the original key into another vector.

### 6.3.1 Intuitive Meaning of the Random Matrix

Each row of S acts like a random directional test.

The first row asks:

“How strongly does the key point along random direction 1?”

The second row asks:

“How strongly does the key point along random direction 2?”

A positive result means that the key points in a similar direction to that row. A negative result means that it points in the opposite direction.

In the example:

random direction 1 → response = −2

random direction 2 → response = 4.25

The projected vector Sk therefore contains the key’s numerical responses to the random directional tests.

QJL uses many of these measurements in practice. One random measurement provides little information, but a large collection of measurements creates a useful statistical fingerprint of the key vector.

### 6.3.2 Sign-Bit Quantisation

After calculating the random projection, QJL keeps only whether each projected value is positive or negative:

QJL(S, k) = sign(Sk)

For the example:

Sk = \[−2, 4.25\]ᵀ

Applying the sign function gives:

sign(Sk) = \[−1, +1\]ᵀ

The exact magnitudes −2 and 4.25 are discarded. QJL stores only:

negative → −1

positive → +1

Because there are only two possible outcomes, each projected coordinate requires one bit.

The complete process is:

Original key:

k = \[2, −1, 4\]ᵀ

Random projection:

Sk = \[−2, 4.25\]ᵀ

Sign-bit quantisation:

sign(Sk) = \[−1, +1\]ᵀ

The resulting sign pattern acts as a compressed fingerprint of the key’s direction.

### 6.3.3 Applying the Same Projection to the Query

When a query vector q arrives, QJL applies the same random matrix:

Sq

The query is not reduced to sign bits. It remains at higher precision.

This means that the query and key have been measured using the same random directions:

key representation = sign(Sk)

query representation = Sq

The estimator then compares the precise query measurements with the stored one-bit key signs.

Using the same matrix is essential. If different random matrices were used for the query and key, their measurements would refer to unrelated directions and could not be compared meaningfully.

So JL creates the random projected sketch; QJL compresses that sketch further into signs. 

> **Archived image:** `d58fb226db8f0c958b49157e1702895a83a0347c.png` is preserved in the controlled provenance ZIP and is not duplicated in Git.


### 

### 6.3.4 What has been lost in the sign sketch?

QJL loses the exact projected magnitudes.

For example it cant distinguish between 0.01 and 10.5 since both become +1.

But the thing is QJL is not trying to reconstruct the exact key, it is just trying to keep a track of whether the key relates to the query or not.

### 6.3.5 Why the One-Bit Sign Pattern Is Useful

After QJL multiplies a key vector by the random projection matrix, it produces a new vector containing *mm*m projected coordinates:

*Sk∈R^m*

QJL then replaces every projected coordinate with either *+1+1*+1 or *−1-1*−1, depending on whether the value is positive or negative:

*sign⁡(Sk)∈{−1,+1}^m*

One individual sign provides very little information. It only shows whether the key points positively or negatively along one random direction. However, when QJL uses many random projected coordinates, the complete pattern of signs becomes a useful statistical fingerprint of the key vector.

For example, a sketch may look like:

*\[+1,−1,+1,+1,−1,…\]*

Each sign represents the key’s response to a different random directional test. When hundreds or thousands of these tests are combined, they provide enough information to estimate how the key relates to the current query.

The important quantity is therefore not only the original key dimension *dd*d, but the number of projected measurements *mm*m. The projection matrix has dimensions:

*S∈R^(m×d)*

where *dd*d is the original key dimension and *mm*m is the number of random measurements. Increasing *mm*m generally provides more evidence and makes the inner-product estimate more reliable. However, it also requires more sign bits and additional computation.

When a query vector arrives, QJL applies the same projection matrix to it:

*Sq*

Unlike the key, the projected query is kept at higher precision. QJL compares the precise query measurements with the stored key signs. For each projected coordinate, the calculation has the form:

*(Sq)\_j×sign⁡((Sk)\_j)*

If the query measurement and the key sign agree, the contribution is positive. If they disagree, the contribution is negative. QJL adds these contributions across all *mm*m projected coordinates.

Many positive contributions suggest that the query and key are aligned. Many negative contributions suggest that they point in opposing directions. A mixture of positive and negative contributions suggests a weaker relationship.

QJL also uses the stored norm of the key:

*∥k∥\_2*

because the one-bit signs preserve directional information but discard the original magnitude of the projected values. The projected query, key sign pattern and key norm are therefore combined to estimate the original query–key inner product:

*q⋅kq\cdot k*q⋅k

This estimate does not directly produce a simple “useful” or “not useful” decision. Instead, it produces an approximate numerical relevance score. A larger score generally indicates that the key is more relevant to the current query. Softmax then compares the estimated scores of all cached keys and assigns larger attention weights to the values associated with the most relevant keys.

In summary, QJL relies on the idea that one sign is weak evidence, but a large collection of independent sign measurements can provide a useful approximation of the relationship between a query and a key. With too few projected coordinates, the result may be noisy. With enough projected coordinates, the sign pattern provides sufficient statistical information to estimate how strongly the key matches the query without storing every original decimal value.

### 6.3.6 Why QJL Stores the Key Norm and How It Differs from Traditional 2-Bit Quantisation

QJL compresses each key vector by first applying a random projection and then keeping only the sign of each projected value:

*k→Sk→sign⁡(Sk)k \rightarrow Sk \rightarrow \operatorname{sign}(Sk)*k→Sk→sign(Sk)

The resulting sign sketch contains only *+1+1*+1 and *−1-1*−1 values. These signs provide information about the direction of the key, but they remove information about its magnitude, or overall size.

For example, consider the following two key vectors:

*k1=\[1,1\]*

k2 =\[100,100\]

Both vectors point in exactly the same direction. The second vector is simply 100 times larger than the first. After applying the same random projection matrix, their projected values will also differ by a factor of 100:

*Sk2=100Sk1*

However, after applying the sign function, both keys may produce the same sign sketch. For example:

*sign⁡(Sk1)=\[+1,−1,+1\]*

*sign⁡(Sk2)=\[+1,−1,+1\]*

The sign sketches show that the two keys point in the same direction, but they do not show that the second key is much larger. This matters because the size of the key affects the query–key inner product and therefore affects the attention score.

To preserve this missing magnitude information, QJL also calculates and stores the Euclidean norm of each key:

*∥k∥\_2*

The Euclidean norm is the overall length of the vector. It is calculated as:


> **Archived image:** `560599546f34797074990ef30c136ddce6df8022.png` is preserved in the controlled provenance ZIP and is not duplicated in Git.


For the two example keys:


> **Archived image:** `73ac89a331b600c8ae3ce28153e3e2c5e00f74a1.png` is preserved in the controlled provenance ZIP and is not duplicated in Git.


Although the sign sketches are identical, the norms show that the second key is 100 times larger.

QJL therefore stores two parts for every key:

*sign⁡(Sk)*

which provides direction-like information, and:

*∥k∥\_2*

which provides magnitude information.

When the current query arrives, QJL applies the same random projection matrix to the query:

*q→Sq*

The projected query remains at higher precision. QJL then compares the precise projected query with the stored key signs and multiplies the result by the stored key norm. The inner-product estimator is:


> **Archived image:** `22702d2e29be5245498fa3bef10bf9f6b20c43d6.png` is preserved in the controlled provenance ZIP and is not duplicated in Git.


The sign sketch indicates how the query and key align across the random projected directions. The norm scales this directional result according to the original size of the key. Therefore, the norm does not reconstruct every missing key coordinate. Instead, it acts as one overall scaling factor when estimating the query–key inner product.

This can be understood as:

*estimated inner product=directional evidence×key magnitude*

This approach differs from traditional 2-bit quantisation. Traditional quantisation normally tries to store an approximate version of every individual coordinate in the key vector. With 2-bit quantisation, each coordinate can be represented using one of four possible codes. However, the codes do not have a fixed meaning unless the system also stores information explaining the range of the original values.

Traditional quantisation therefore usually divides the key into blocks and stores additional information for each block, such as:

- a scale;

- a zero point.

The approximate value may then be reconstructed using a formula such as:


> **Archived image:** `d5e3f9b5fc7e1ab5dd642c1408d03307dccd624c.png` is preserved in the controlled provenance ZIP and is not duplicated in Git.


These extra values are normally stored at higher precision. As a result, a method described as 2-bit quantisation may require more than two effective bits per original value once the scales and zero points are included.

QJL avoids storing separate scales and zero points for many small blocks. Instead, it stores one-bit signs for the projected key and one norm for the whole key. The random projection matrix is shared across all keys and does not need to be stored again for each token.

The difference can be summarised as follows:

*Traditional 2-bit quantisation=2-bit codes+scales+zero points*

QJL=1-bit projected signs+one key norm

QJL is designed specifically for estimating query–key inner products. It does not spend memory trying to reconstruct every original key coordinate accurately. This makes it suitable for the key cache, where the main requirement is to estimate attention scores.

However, QJL is not automatically more efficient in every possible system. Its practical efficiency depends on the number of projected measurements *mm*m, the cost of performing the random projection, the hardware implementation and the required accuracy. If *m* is too large, the sign sketch may use more memory and computation. The main advantage is that QJL directs its limited number of bits towards preserving the inner-product information needed by attention, while avoiding repeated block-level scale and zero-point values.

The phrase “zero overhead” in the QJL paper should therefore not be interpreted as meaning that no additional information is stored. QJL still stores the norm of each key. The phrase means that it avoids the traditional quantisation overhead caused by storing scales and zero points for multiple data blocks.

### 6.3.7 Does QJL restore the original inner product exactly?

No.

It produces an estimate:

*q⋅k^*

It does not guarantee:

*q⋅k^=q⋅k*

for every individual query and key.

The paper proves two more careful properties.

### 6.3.8 Bounded distortion

The paper also proves that with enough projected measurements *mm*m, the probability of a large error becomes small.

More random measurements generally mean:

- more one-bit evidence

- a more stable estimate

- more memory and computation

The unbiased result is Lemma 3.2 on page 5, and the error bound is Lemma 3.5 on page 6.

### 6.3.9 What happens to the query?

Whilst the key is heavily compressed to one-bit signs, the query is kept at a higher precision.

For example:

*Sq=\[0.91,−0.34,1.27,−0.42\]*

while the key may be stored as:

*sign⁡(Sk)=\[+1,−1,+1,−1\]*

It doesn’t cause any overhead issues since keeping one query precise is inexpensive.

### 6.3.10 What happens for all the keys?


> **Archived image:** `956c3b48237de8ace1b6af462138b6de83941b93.png` is preserved in the controlled provenance ZIP and is not duplicated in Git.


### 6.3.11 What happens to the value vectors?

In the standalone QJL paper, QJL is mainly used for the key cache.

The values use standard token-wise quantisation.

The reason is that the two cache components perform different jobs:

- keys are involved in sensitive query–key inner products before softmax

- values are multiplied by the resulting attention weights afterward

The paper states on pages 3 and 7 that standard token-wise quantisation is used for values because it is already effective for that part of attention.

## 6.4 The entire QJL Process


> **Archived image:** `ddd9d8a7c5d198cd65440aeb338f5fd0981ba734.png` is preserved in the controlled provenance ZIP and is not duplicated in Git.


Overall, QJL asks each key m random directional questions, stores only the positive-or-negative answers and the key’s overall length, then compares those answers with a precise projection of the current query to estimate the original attention scores.
