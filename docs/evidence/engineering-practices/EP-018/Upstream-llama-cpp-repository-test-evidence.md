# EP-018 â€” Upstream Repository-Test Infrastructure Evidence

## Practice demonstrated

The project used controlled run identifiers, immutable raw logs, separate
stdout and stderr, run manifests, a failure record, a corrective retest, and a
complete repository test suite against the exact pinned build.

## Results

- `UL-B04-R001` preserved a 51/52 result and the missing-Jinja2 diagnosis.
- `UL-B04-R002` preserved a 52/52 passing result.
- The failed run was not overwritten or hidden.
- The corrective dependency was isolated from the project repository.
- No model or inference claim was made.

## Remaining EP-018 work

This evidence completes the upstream repository-test portion of EP-018.
EP-018 remains in progress until the GraniteEdgeAI application repository also
contains:

1. a real automated C# test project;
2. controlled small fixtures and a fixture manifest;
3. one repeatable local repository validation command;
4. a Windows GitHub Actions workflow;
5. retained CI test output and one green clean-checkout run.

Later physical test results will use this completed infrastructure rather than
reopen the preparation task.
