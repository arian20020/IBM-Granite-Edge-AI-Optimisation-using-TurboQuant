void turboq_quantize_head_qjl(const float* src, unsigned char* dst) {
    SignCodec sign_codec;
}
float turboq_codec_qk_dot_qjl(const unsigned char* record, const float* query) {
    return SignCodec::dot(record, query);
}
