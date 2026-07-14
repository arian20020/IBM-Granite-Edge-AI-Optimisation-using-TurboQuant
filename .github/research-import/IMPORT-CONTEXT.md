# Controlled research import context

This branch temporarily stores a checksum-verified compressed payload so GitHub Actions can materialise the curated research library as normal files under `/research`.

The materialisation workflow validates the archive SHA-256 and expected file counts, extracts the real research tree, and deletes this temporary import directory before the branch is merged.
