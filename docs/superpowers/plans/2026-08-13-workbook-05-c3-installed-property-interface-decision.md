# C3 Installed Cache-Property Interface Decision

**Status:** Authoritative C3 correction  
**Implementation status:** Not started

The accepted Runtime install log confirms that the public header `runtime/include/openvino/runtime/properties.hpp` was installed. The same retained log contains no installed `openvino/runtime/internal_properties.hpp` development header.

The accepted-build C3 probe therefore must not include the unavailable internal header or construct `ov::internal::CacheQuantAlgorithm` values directly.

The exact accepted OpenVINO source tests use installed-compatible string keys and serialized values in `ov::AnyMap`. The probe follows that pattern independently for K and V:

```cpp
ov::AnyMap properties;
properties["KEY_CACHE_PRECISION"] = "u3";
properties["KEY_CACHE_QUANT_ALG"] = "TURBO";
properties["VALUE_CACHE_PRECISION"] = "u4";
properties["VALUE_CACHE_QUANT_ALG"] = "SCALAR";
```

C3 contract tests must compile this adapter against the accepted installed Runtime and GenAI headers and libraries before any model execution. Property acceptance, dispatch, fallback, and storage proof remain separate; successful compilation or property serialization does not prove TurboQuant activation.

The separately labelled exact-source trace build may include development headers inside its own source workspace. That exception does not alter the accepted-build probe interface and does not make trace results eligible for formal performance or quality claims.

This decision overrides the internal-enum example in the C3 package plan. All other C3 requirements remain unchanged.