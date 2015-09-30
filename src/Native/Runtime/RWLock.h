//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

class ReaderWriterLock
{
    volatile Int32  m_RWLock;       // lock used for R/W synchronization
    Int32           m_spinCount;    // spin count for a reader waiting for a writer to release the lock

#if 0
    // used to prevent writers from being starved by readers
    // we currently do not prevent writers from starving readers since writers 
    // are supposed to be rare.
    bool            m_WriterWaiting;
#endif

    bool TryAcquireReadLock();
    bool TryAcquireWriteLock();

public:
    class ReadHolder
    {
        ReaderWriterLock * m_pLock;
        bool               m_fLockAcquired;
    public:
        ReadHolder(ReaderWriterLock * pLock, bool fAcquireLock = true);
        ~ReadHolder();
    };

    class WriteHolder
    {
        ReaderWriterLock * m_pLock;
        bool               m_fLockAcquired;
    public:
        WriteHolder(ReaderWriterLock * pLock, bool fAcquireLock = true);
        ~WriteHolder();
    };

    ReaderWriterLock();

    void AcquireReadLock();
    void ReleaseReadLock();

    bool DangerousTryPulseReadLock();

protected:
    void AcquireWriteLock();
    void ReleaseWriteLock();

    void AcquireReadLockWorker();

};

