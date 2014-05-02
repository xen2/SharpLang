#include <stdint.h>
#include <stdlib.h>

void* allocObject(uint32_t size)
{
    return malloc(size);
}
