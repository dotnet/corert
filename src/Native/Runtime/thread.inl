// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#ifndef DACCESS_COMPILE
inline void Thread::SetCurrentThreadPInvokeTunnelForGcAlloc(void * pTransitionFrame)
{
    ASSERT(ThreadStore::GetCurrentThread() == this);
    ASSERT(Thread::IsCurrentThreadInCooperativeMode());
    m_pHackPInvokeTunnel = pTransitionFrame;
}

inline void Thread::SetupHackPInvokeTunnel()
{
    ASSERT(ThreadStore::GetCurrentThread() == this);
    ASSERT(!Thread::IsCurrentThreadInCooperativeMode());
    m_pHackPInvokeTunnel = m_pTransitionFrame;
}
#endif // DACCESS_COMPILE
