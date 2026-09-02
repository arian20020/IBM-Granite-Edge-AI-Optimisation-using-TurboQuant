enum class CacheCodec {
    TURBO_QUANT_3,
    TURBO_QUANT_4,
    TURBO_QUANT_3_QJL,  // not yet supported
    TURBO_QUANT_4_QJL,  // not yet supported
    POLAR_QUANT_3,
    POLAR_QUANT_4,
};
static constexpr Property<CacheCodec> key_cache_codec{"KEY_CACHE_CODEC"};
static constexpr Property<CacheCodec> value_cache_codec{"VALUE_CACHE_CODEC"};
