// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

EXTERN_C DECLSPEC_THREAD ThreadBuffer tls_CurrentThread;

// static
inline Thread * ThreadStore::RawGetCurrentThread()
{
    return (Thread *) &tls_CurrentThread;
}

// static
inline Thread * ThreadStore::GetCurrentThread()
{
    Thread * pCurThread = RawGetCurrentThread();

    // If this assert fires, and you only need the Thread pointer if the thread has ever previously
    // entered the runtime, then you should be using GetCurrentThreadIfAvailable instead.
    ASSERT(pCurThread->IsInitialized());    
    return pCurThread;
}

// static
inline Thread * ThreadStore::GetCurrentThreadIfAvailable()
{
    Thread * pCurThread = RawGetCurrentThread();
    if (pCurThread->IsInitialized())
        return pCurThread;

    return NULL;
}

EXTERN_C volatile UInt32 RhpTrapThreads;

// static
inline bool ThreadStore::IsTrapThreadsRequested()
{
    return (RhpTrapThreads & (UInt32)TrapThreadsFlags::TrapThreads) != 0;
}
