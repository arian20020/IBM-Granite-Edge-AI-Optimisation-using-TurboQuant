namespace GraniteEdgeAI.Features.ModelImport.Selection;

/// <summary>
/// The bounded, path-free facts established while classifying a source-model
/// directory. It is intentionally data-only: conversion is a later route.
/// </summary>
internal sealed record SourceModelSelectionPreflight(
    bool HasRootConfig,
    bool HasWeightFileOrIndex,
    bool HasCompleteIndexedShards,
    bool RequiresCustomCode);
