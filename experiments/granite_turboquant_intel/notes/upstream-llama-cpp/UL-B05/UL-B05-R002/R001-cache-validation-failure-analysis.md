# UL-B05-R001 Cache Validation False Negative

The R001 CMake configure process exited successfully and its cache contains all seven required values. The post-configure validator nevertheless classified every check as missing because each regex ended with `$` while `CMakeCache.txt` uses CRLF line endings; the unmatched `\r` precedes each `\n`.

R002 preserves the same valid build tree and revalidates with `\r?$` line endings before compilation. R001 remains preserved as a harness failure and is not reclassified as a successful run.
