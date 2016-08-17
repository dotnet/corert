// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
class Thread;
class CLREventStatic;
class RuntimeInstance;
class Array;
typedef DPTR(RuntimeInstance) PTR_RuntimeInstance;

class ThreadStore
{
    SList<Thread>       m_ThreadList;
    PTR_RuntimeInstance m_pRuntimeInstance;
    CLREventStatic      m_SuspendCompleteEvent;
    ReaderWriterLock    m_Lock;

private:
    ThreadStore();

    void                    LockThreadStore();
    void                    UnlockThreadStore();

public:
    class Iterator
    {
        ReaderWriterLock::ReadHolder    m_readHolder;
        PTR_Thread                      m_pCurrentPosition;
    public:
        Iterator();
        ~Iterator();
        PTR_Thread GetNext();
    };

    ~ThreadStore();
    static ThreadStore *    Create(RuntimeInstance * pRuntimeInstance);
    static Thread *         RawGetCurrentThread();
    static Thread *         GetCurrentThread();
    static Thread *         GetCurrentThreadIfAvailable();
    static PTR_Thread       GetSuspendingThread();
    static void             AttachCurrentThread();
    static void             AttachCurrentThread(bool fAcquireThreadStoreLock);
    static void             DetachCurrentThread();
#ifndef DACCESS_COMPILE
    static void             SaveCurrentThreadOffsetForDAC();
#else
    static PTR_Thread       GetThreadFromTEB(TADDR pvTEB);
#endif
    Boolean                 GetExceptionsForCurrentThread(Array* pOutputArray, Int32* pWrittenCountOut);

    void        Destroy();
    void        SuspendAllThreads(CLREventStatic* pCompletionEvent);
    void        ResumeAllThreads(CLREventStatic* pCompletionEvent);

    static bool IsTrapThreadsRequested();
    void        WaitForSuspendComplete();
};
typedef DPTR(ThreadStore) PTR_ThreadStore;

ThreadStore * GetThreadStore();


#define FOREACH_THREAD(p_thread_name)                       \
{                                                           \
    ThreadStore::Iterator __threads;                        \
    Thread * p_thread_name;                                 \
    while ((p_thread_name = __threads.GetNext()) != NULL)   \
    {                                                       \

#define END_FOREACH_THREAD  \
    }                       \
}                           \

