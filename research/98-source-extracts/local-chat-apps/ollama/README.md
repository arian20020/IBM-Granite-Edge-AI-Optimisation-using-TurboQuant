# Ollama

## What it is

Ollama is a local model runner and model-management system.

## It allows users to:

- Download language models.

- Load and run them locally

- Chat through a terminal or desktop interface

- Customise model behaviour

- Expose models through a local API

- Connect models to apps like VS code and coding agents

- Import and quantise compatible models

# How Ollama differs from LM Studio

| **LM Studio**                          | **Ollama**                                  |
|----------------------------------------|---------------------------------------------|
| Primarily graphical                    | Primarily command-line and API focused      |
| Built-in polished chat workspace       | Simpler model-running interface             |
| Manages models visually                | Manages models using commands               |
| Local API available                    | Local API is a central part of the platform |
| Good for exploring model settings      | Good for integrating models into software   |
| Usually downloads pre-quantised models | Can download, import and quantise models    |
| Hides more technical detail            | Exposes a simpler developer workflow        |

Ollama can quantise compatible FP16 and FP32 models itself. It supports the ollama create --quantize workflow, although that is separate from TurboQuant and is not required for our first experiment.

# Important Ollama concepts

## Model name

An Ollama model name may look like:

granite4:3b

Here:

granite4 = model family  
3b = model tag or variant

## Model tag

The tag identifies a particular model version or size.

Examples include:

granite4:350m  
granite4:1b  
granite4:3b  
granite4:3b-h  
granite4:7b-a1b-h  
granite4:32b-a9b-h

## Ollama server

The Ollama application runs a background server that loads models and accepts requests from the terminal or other applications.

## Modelfile

A Modelfile is a configuration blueprint used to create a customised Ollama model. It can define:

- the underlying model;

- temperature;

- context length;

- system prompt;

- prompt template;

- adapters;

- example messages.

Ollama describes a Modelfile as the blueprint for creating and sharing customised models.

Example:

FROM granite4:3b  

PARAMETER temperature 0.2  
PARAMETER num_ctx 4096  

SYSTEM """  
You are a patient computer science tutor.  
Explain technical concepts using simple English.  
"""

## Local API

My application can send requests to Ollama rather than implementing model inference directly:

Your user interface  
↓  
HTTP request  
↓  
Ollama API  
↓  
Granite model  
↓  
Response

This is one reason Ollama is useful to study before directly integrating llama.cpp.

# Is Ollama local or cloud-based?

Ollama supports both local and cloud models.
