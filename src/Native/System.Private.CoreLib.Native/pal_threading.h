// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma once

#include "pal_common.h"

#include <stdlib.h>

#include <pthread.h>

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// LowLevelMutex

// Wraps a non-recursive mutex
class LowLevelMutex
{
protected:
    pthread_mutex_t m_mutex;

#if DEBUG
private:
    bool m_isLocked;
#endif

public:
    LowLevelMutex(bool abortOnFailure, bool *successRef)
#if DEBUG
        : m_isLocked(false)
#endif
    {
        assert(abortOnFailure || successRef != nullptr);

        int error = pthread_mutex_init(&m_mutex, nullptr);
        if (error != 0)
        {
            if (abortOnFailure)
            {
                abort();
            }
            *successRef = false;
            return;
        }

        if (successRef != nullptr)
        {
            *successRef = true;
        }
    }

    ~LowLevelMutex()
    {
        int error = pthread_mutex_destroy(&m_mutex);
        assert(error == 0);

        UnusedInRelease(error);
    }

protected:
    void SetIsLocked(bool isLocked)
    {
#if DEBUG
        assert(m_isLocked != isLocked);
        m_isLocked = isLocked;
#endif
    }

public:
    void Acquire()
    {
        int error = pthread_mutex_lock(&m_mutex);
        assert(error == 0);
        SetIsLocked(true);

        UnusedInRelease(error);
    }

    bool TryAcquire()
    {
        int error = pthread_mutex_trylock(&m_mutex);
        assert(error == 0 || error == EBUSY);
        if (error == 0)
        {
            SetIsLocked(true);
        }
        return error == 0;
    }

    void Release()
    {
        SetIsLocked(false);
        int error = pthread_mutex_unlock(&m_mutex);
        assert(error == 0);

        UnusedInRelease(error);
    }

    LowLevelMutex(const LowLevelMutex &other) = delete;
    LowLevelMutex &operator =(const LowLevelMutex &other) = delete;
};

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// LowLevelMonitor

// Wraps a non-recursive mutex and condition
class LowLevelMonitor final : public LowLevelMutex
{
private:
    pthread_cond_t m_condition;

public:
    LowLevelMonitor(bool abortOnFailure, bool *successRef);

    ~LowLevelMonitor()
    {
        int error = pthread_cond_destroy(&m_condition);
        assert(error == 0);

        UnusedInRelease(error);
    }

public:
    void Wait()
    {
        SetIsLocked(false);
        int error = pthread_cond_wait(&m_condition, &m_mutex);
        assert(error == 0);
        SetIsLocked(true);

        UnusedInRelease(error);
    }

    bool Wait(int32_t timeoutMilliseconds);

public:
    void Signal()
    {
        int error = pthread_cond_signal(&m_condition);
        assert(error == 0);

        UnusedInRelease(error);
    }

    void SignalAll()
    {
        int error = pthread_cond_broadcast(&m_condition);
        assert(error == 0);

        UnusedInRelease(error);
    }

    LowLevelMonitor(const LowLevelMonitor &other) = delete;
    LowLevelMonitor &operator =(const LowLevelMonitor &other) = delete;
};
