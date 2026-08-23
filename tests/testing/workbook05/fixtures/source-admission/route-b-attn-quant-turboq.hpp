void polarq_quantize_head(const float* src, unsigned char* dst);
float polarq_fused_qk_dot(const unsigned char* record, const float* query);
void polarq_fused_v_accum(const unsigned char* record, float weight, float* output);
int polarq_head_bytes(int dimension, int bits);
