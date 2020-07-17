// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#ifndef __GcStressControl_h__
#define __GcStressControl_h__


enum HijackType { htLoop, htCallsite };
bool ShouldHijackForGcStress(UIntNative CallsiteIP, HijackType ht);


enum GcStressThrottleMode {
        gcstm_TriggerAlways     = 0x0000,   // trigger a GC every time we hit a GC safe point
        gcstm_TriggerOnFirstHit = 0x0001,   // trigger a GC the first time a GC safe point is hit
        gcstm_TriggerRandom     = 0x0002,   // trigger a GC randomly, as defined by GcStressFreqCallsite/GcStressFreqLoop/GcStressSeed
};

struct CallsiteCountEntry
{
    UIntNative callsiteIP;
    UIntNative countHit;
    UIntNative countForced;
    HijackType ht;
};

typedef DPTR(CallsiteCountEntry) PTR_CallsiteCountEntry;

class CallsiteCountTraits: public NoRemoveSHashTraits< DefaultSHashTraits < CallsiteCountEntry > >
{
public:
    typedef UIntNative key_t;

    static UIntNative GetKey(const CallsiteCountEntry & e) { return e.callsiteIP; }

    static count_t Hash(UIntNative k)
    { return (count_t) k; }

    static bool Equals(UIntNative k1, UIntNative k2)
    { return k1 == k2; }

    static CallsiteCountEntry Null() 
    { CallsiteCountEntry e; e.callsiteIP = 0; return e; }

    static bool IsNull(const CallsiteCountEntry & e)
    { return e.callsiteIP == 0; }
};

typedef SHash < CallsiteCountTraits > CallsiteCountSHash;
typedef DPTR(CallsiteCountSHash) PTR_CallsiteCountSHash;


#endif // __GcStressControl_h__
