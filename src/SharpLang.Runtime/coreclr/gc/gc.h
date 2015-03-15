//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//


/*++

Module Name:

    gc.h

--*/

#ifndef __GC_H
#define __GC_H

#include "gcenv.h"

// !!!!!!!!!!!!!!!!!!!!!!!
// make sure you change the def in bcl\system\gc.cs 
// if you change this!
enum collection_mode
{
    collection_non_blocking = 0x00000001,
    collection_blocking = 0x00000002,
    collection_optimized = 0x00000004,
    collection_compacting = 0x00000008
#ifdef STRESS_HEAP
    , collection_gcstress = 0x80000000
#endif // STRESS_HEAP
};

class GCHeap;
GPTR_DECL(GCHeap, g_pGCHeap);

//constants for the flags parameter to the gc call back

#define GC_CALL_INTERIOR            0x1
#define GC_CALL_PINNED              0x2
#define GC_CALL_CHECK_APP_DOMAIN    0x4

//flags for GCHeap::Alloc(...)
#define GC_ALLOC_FINALIZE 0x1
#define GC_ALLOC_CONTAINS_REF 0x2
#define GC_ALLOC_ALIGN8_BIAS 0x4

class GCHeap
{
public:
    static GCHeap *GetGCHeap()
    {
        return g_pGCHeap;
    }

    virtual void    SetFinalizationRun (Object* obj) = 0;

    virtual HRESULT GarbageCollect (int generation = -1, BOOL low_memory_p=FALSE, int mode = collection_blocking) = 0;

    virtual int CollectionCount (int generation, int get_bgc_fgc_count = 0) = 0;

        // Finalizer queue stuff (should stay)
    virtual bool    RegisterForFinalization (int gen, Object* obj) = 0;
    
        // General queries to the GC
    virtual BOOL    IsPromoted (Object *object) = 0;
    virtual unsigned WhichGeneration (Object* object) = 0;

    virtual int GetGcLatencyMode() = 0;
    virtual int SetGcLatencyMode(int newLatencyMode) = 0;

    virtual int GetLOHCompactionMode() = 0;
    virtual void SetLOHCompactionMode(int newLOHCompactionyMode) = 0;

    virtual BOOL RegisterForFullGCNotification(DWORD gen2Percentage, 
                                               DWORD lohPercentage) = 0;
    virtual BOOL CancelFullGCNotification() = 0;
    virtual int WaitForFullGCApproach(int millisecondsTimeout) = 0;
    virtual int WaitForFullGCComplete(int millisecondsTimeout) = 0;

    virtual size_t  GetTotalBytesInUse () = 0;

    static unsigned GetMaxGeneration() {
        LIMITED_METHOD_DAC_CONTRACT;  
        return max_generation;
    }

private:
    enum {
        max_generation  = 2,
    };
};

#endif
