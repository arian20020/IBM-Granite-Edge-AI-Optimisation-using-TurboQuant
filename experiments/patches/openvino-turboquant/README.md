# OpenVINO GenAI TurboQuant patch set

This directory contains the portable project patch set applied to the immutable
OpenVINO GenAI `2026.2.1.0` baseline at commit
`7dea0459b2ac7d8dfd877fd9df6737674fd8371d`.

Patch files must use the `.patch` suffix. The workspace controller applies them
in lexical filename order to a local, no-hardlink clone on branch
`project/turboquant-wb04`. It refuses a dirty upstream or destination and writes
source identity evidence beside the derived checkout. Project-added TurboQuant
functionality must remain identified separately from upstream OpenVINO support.
