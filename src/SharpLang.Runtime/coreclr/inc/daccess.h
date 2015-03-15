#ifndef __daccess_h__
#define __daccess_h__

typedef ULONG_PTR TADDR;
typedef const void* PTR_CVOID;
typedef void* PTR_VOID;

#define DPTR(type) type*
#define ArrayDPTR(type) type*
#define SPTR(type) type*

template <typename Tgt, typename Src>
inline Tgt dac_cast(Src src)
{
    return (Tgt)(src);
}

#endif
