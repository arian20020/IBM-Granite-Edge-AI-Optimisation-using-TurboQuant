# CI Build-and-Test Workflow

**Version:** 1.0  
**Document type:** Engineering process view  
**Related engineering practice:** EP-018  
**Owner:** Project developer  
**Last reviewed:** 16 July 2026  
**Source of truth:** `.github/workflows/build-and-test.yml`  
**Status:** Implemented locally; GitHub-hosted validation pending  

## 1. Purpose

This document describes how the repository automatically restores,
builds and tests the Windows application after a branch push, a pull
request targeting `main`, or a manually requested workflow run.

This is a continuous-integration process diagram. It does not replace
the application's context, component, runtime or deployment
architecture views.

## 2. Current implemented workflow

```mermaid
flowchart TD
    A["Branch push, pull request to main,<br/>or manual workflow run"]
    B["GitHub Actions<br/>Windows runner"]
    C["Check out exact repository revision"]
    D["Install .NET SDK<br/>from global.json"]
    E["Configure x64 MSBuild"]
    F["Restore WinUI application"]
    G["Restore MSTest project"]
    H["Build WinUI application<br/>Release x64"]
    I{"Application build succeeded?"}
    J["Build unit-test project<br/>Release"]
    K{"Test project build succeeded?"}
    L["Run unit tests<br/>Microsoft Testing Platform"]
    M["Generate TRX test report"]
    N["Upload available TRX artifact<br/>30-day retention"]
    O{"All unit tests passed?"}
    P["CI status: passed"]
    Q["CI status: failed"]

    A --> B
    B --> C
    C --> D
    D --> E
    E --> F
    F --> G
    G --> H
    H --> I

    I -- "No" --> Q
    I -- "Yes" --> J

    J --> K
    K -- "No" --> Q
    K -- "Yes" --> L

    L --> M
    M --> N
    L --> O

    O -- "Yes" --> P
    O -- "No" --> Q
```
