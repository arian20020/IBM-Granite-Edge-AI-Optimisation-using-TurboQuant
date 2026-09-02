enum class CacheQuantAlgorithm {
    SCALAR = 0,
    TURBO = 1,
};
static constexpr Property<CacheQuantAlgorithm> key_cache_quant_alg{"KEY_CACHE_QUANT_ALG"};
static constexpr Property<CacheQuantAlgorithm> value_cache_quant_alg{"VALUE_CACHE_QUANT_ALG"};
