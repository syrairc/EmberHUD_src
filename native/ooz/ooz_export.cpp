#include <stddef.h>
#include <stdint.h>

int Kraken_Decompress(const uint8_t *src, size_t src_len, uint8_t *dst, size_t dst_len);

extern "C" __declspec(dllexport) int ooz_decompress(const uint8_t *src, size_t src_len, uint8_t *dst, size_t dst_len)
{
    return Kraken_Decompress(src, src_len, dst, dst_len);
}
