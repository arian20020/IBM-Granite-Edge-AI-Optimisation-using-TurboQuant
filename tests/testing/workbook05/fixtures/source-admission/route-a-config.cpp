if (key == ov::key_cache_precision.name()) {
    if (one_of(prec, ov::element::u3, ov::element::u4)) {
        keyCachePrecision = prec;
    }
}
if (key == ov::value_cache_precision.name()) {
    if (one_of(prec, ov::element::u3, ov::element::u4)) {
        valueCachePrecision = prec;
    }
}
