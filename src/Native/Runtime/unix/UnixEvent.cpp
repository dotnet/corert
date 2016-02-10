// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// Implementation of events for Unix
//

#include "CommonTypes.h"
#include "PalRedhawkCommon.h"
#include "CommonMacros.h"
#include "sal.h"
#include "config.h"
#include "PalRedhawkInline.h"
#include "UnixCommon.h"
#include <errno.h>
#include <stdio.h>
#include <string.h>
#include <pthread.h>
#include <sys/time.h>
#ifdef __APPLE__
#include <mach/mach.h>
#include <mach/mach_time.h>
#endif // __APPLE__
#include "daccess.h"
#include "slist.h"
#include "slist.inl"

#include "UnixEvent.h"

static const uint32_t INFINITE = 0xFFFFFFFF;

#define WAIT_OBJECT_0           0
#define WAIT_TIMEOUT            258
#define WAIT_FAILED             0xFFFFFFFF

#ifdef __APPLE__
// Convert nanoseconds to the timespec structure
// Parameters:
//  nanoseconds - time in nanoseconds to convert
//  t           - the target timespec structure
void NanosecondsToTimespec(uint64_t nanoseconds, timespec* t)
{
    t->tv_sec = nanoseconds / tccSecondsToNanoSeconds;
    t->tv_nsec = nanoseconds % tccSecondsToNanoSeconds;
}
#endif // __APPLE__

UnixEventWaiter::UnixEventWaiter(uint32_t eventCount)
: m_eventIndex(-1),
  m_timedOutOrFailed(false),
  m_eventCount(eventCount)
{
    if (eventCount <= MaximumEmbeddedEvents)
    {
        m_waiterListEntries = m_embeddedWaiterListEntries;
    }
    else
    {
        FATAL_ASSERT(eventCount <= WAIT_TIMEOUT, "Too many events to wait on");

        m_waiterListEntries = (ListEntry*)malloc(eventCount * sizeof(ListEntry));
        FATAL_ASSERT(m_waiterListEntries != NULL, "Out of memory allocating m_waiterListEntries array");
    }

    int st = pthread_mutex_init(&m_mutex, NULL);
    FATAL_ASSERT(st == 0, "Failed to initialize UnixEventWaiter mutex");

    pthread_condattr_t attrs;
    st = pthread_condattr_init(&attrs);
    FATAL_ASSERT(st == 0, "Failed to initialize UnixEventWaiter condition attribute");

#if HAVE_CLOCK_MONOTONIC
    // Ensure that the pthread_cond_timedwait will use CLOCK_MONOTONIC
    st = pthread_condattr_setclock(&attrs, CLOCK_MONOTONIC);
    FATAL_ASSERT(st == 0, "Failed to set UnixEventWaiter condition variable wait clock");
#endif // HAVE_CLOCK_MONOTONIC

    st = pthread_cond_init(&m_condition, &attrs);
    FATAL_ASSERT(st == 0, "Failed to initialize UnixEventWaiter condition variable");

    st = pthread_condattr_destroy(&attrs);
    FATAL_ASSERT(st == 0, "Failed to destroy UnixEventWaiter condition attribute");
}

UnixEventWaiter::~UnixEventWaiter()
{
    int st = pthread_mutex_destroy(&m_mutex);
    FATAL_ASSERT(st == 0, "Failed to destroy UnixEventWaiter mutex");

    st = pthread_cond_destroy(&m_condition);
    FATAL_ASSERT(st == 0, "Failed to destroy UnixEventWaiter condition variable");

    if (m_waiterListEntries != m_embeddedWaiterListEntries)
    {
        free(m_waiterListEntries);
    }
}

// Add event to the waiter
// Parameters:
//  index - Index of the event that the waiter reports when the event is signalled
//  event - Event to wait on
void UnixEventWaiter::AddEvent(int index, UnixEvent* event)
{
    ASSERT(index < m_eventCount);
    m_waiterListEntries[index] = ListEntry(this);
    event->AddWaiter(&m_waiterListEntries[index]);
}

// Remove event from the waiter
// Parameters:
//  index - Index of the event to remove
//  event - Event to remove
void UnixEventWaiter::RemoveEvent(int index, UnixEvent* event)
{
    ASSERT(index < m_eventCount);
    event->RemoveWaiter(&m_waiterListEntries[index]);
}

// Signal the waiter that an attached event was set. It is called 
// by the UnixEvent instances. 
// Parameters:
//  index - index of the event in the list of events managed by this waiter
// Return:
//  true if the signal has caused the wait to complete, false if the wait
//  completion was already triggered by another signal.
bool UnixEventWaiter::Signal(int index)
{
	bool releasedWait = false;
    pthread_mutex_lock(&m_mutex);
    // Only the first signal is used to release the waiting thread. 
    // If the wait has already timed out or failed, we don't consume the signal.
    // This is important for autoreset events
    if (!m_timedOutOrFailed && (m_eventIndex == NoEventIndex))
    {
	    m_eventIndex = index;
	    releasedWait = true;
	    // Unblock the thread waiting for the condition variable
	    pthread_cond_signal(&m_condition);
	}
    pthread_mutex_unlock(&m_mutex);

    return releasedWait;
}

// Wait for one of the events attached to this waiter. It completes when
// either one of the events is set or the wait timeouts.
// Parameters:
//  milliseconds - wait timeout
// Return:
//  One of the following values:
//      WAIT_OBJECT_0 + n - the wait completed due to the event with index n
//      WAIT_TIMEOUT      - the wait timed out
//      WAIT_FAILED       - the wait has failed due to some system related issue
uint32_t UnixEventWaiter::Wait(uint32_t milliseconds)
{
    timespec endTime;
#ifdef __APPLE__
    uint64_t endMachTime;
    mach_timebase_info_data_t timeBaseInfo;
#endif

    if (milliseconds != INFINITE)
    {
#if HAVE_CLOCK_MONOTONIC
        clock_gettime(CLOCK_MONOTONIC, &endTime);
        TimeSpecAdd(&endTime, milliseconds);
#else // HAVE_CLOCK_MONOTONIC

#ifdef __APPLE__
        uint64_t nanoseconds = (uint64_t)milliseconds * tccMilliSecondsToNanoSeconds;
        NanosecondsToTimespec(nanoseconds, &endTime);
        mach_timebase_info(&timeBaseInfo);
        endMachTime =  mach_absolute_time() + nanoseconds * timeBaseInfo.denom / timeBaseInfo.numer;
#else // __APPLE__
#error Cannot perform reliable wait for pthread condition on this platform
#endif // __APPLE__

#endif // HAVE_CLOCK_MONOTONIC
    }

    int st = 0;

    pthread_mutex_lock(&m_mutex);
    while (m_eventIndex == NoEventIndex)
    {
        if (milliseconds == INFINITE)
        {
            st = pthread_cond_wait(&m_condition, &m_mutex);
        }
        else
        {
#ifdef __APPLE__
            // Since OSX doesn't support CLOCK_MONOTONIC, we use relative variant of the 
            // timed wait and we need to handle spurious wakeups properly.
            st = pthread_cond_timedwait_relative_np(&m_condition, &m_mutex, &endTime);
            if ((st == 0) && (m_eventIndex != NoEventIndex))
            {
                uint64_t machTime = mach_absolute_time();
                if (machTime < endMachTime)
                {
                    // The wake up was spurious, recalculate the relative endTime
                    uint64_t remainingNanoseconds = (endMachTime - machTime) * timeBaseInfo.numer / timeBaseInfo.denom;
                    NanosecondsToTimespec(remainingNanoseconds, &endTime);
                }
                else
                {
                    // Although the timed wait didn't report a timeout, time calculated from the
                    // mach time shows we have already reached the end time. It can happen if
                    // the wait was spuriously woken up right before the timeout.
                    st = ETIMEDOUT;
                }
            }
#else // __APPLE__ 
            st = pthread_cond_timedwait(&m_condition, &m_mutex, &endTime);
            // Verify that if the wait timed out, the event was not set
            ASSERT((st != ETIMEDOUT) || (m_eventIndex == NoEventIndex));
#endif // __APPLE__
        }

        if (st != 0)
        {
            m_timedOutOrFailed = true;
            break;
        }
    }

    pthread_mutex_unlock(&m_mutex);

    uint32_t waitStatus;

    if (st == 0)
    {
    	ASSERT(m_eventIndex != NoEventIndex);
        waitStatus = WAIT_OBJECT_0 + m_eventIndex;
    }
    else if (st == ETIMEDOUT)
    {
        waitStatus = WAIT_TIMEOUT;
    }
    else
    {
        waitStatus = WAIT_FAILED;
    }
    return waitStatus;
}

// Signal a waiter that the event was set
// Parameters:
//  index  - index of the current event in the list of events associated with the waiter
//  waiter - the waiter to signal
void UnixEvent::SignalWaiter(int index, UnixEventWaiter* waiter)
{
    if (waiter->Signal(index) && !m_manualReset)
    {
    	// Autoreset event released a waiter, so we need to clear
    	// the event state
    	m_state = false;
    }	
}

// Set the event to signalled state
void UnixEvent::Set()
{
    pthread_mutex_lock(&m_mutex);
    // Signal waiters only when the event transitions from not set to set.
    if (!m_state)
    {
        m_state = true;

        // Pass the event to all waiters for manual reset event
        // or to the first waiter for autoreset event.
        for (SList<UnixEventWaiter::ListEntry>::Iterator it = m_waiters.Begin(); m_state && it != m_waiters.End(); it++)
        {
            SignalWaiter(it->GetEventIndex(), it->GetWaiter());
        }
    }
    pthread_mutex_unlock(&m_mutex);
}

// Reset the event state to non-signalled
void UnixEvent::Reset()
{
    pthread_mutex_lock(&m_mutex);
    m_state = false;
    pthread_mutex_unlock(&m_mutex);
}

// Add waiter to the list of waiters waiting for the signal
// Parameters:
//  index  - index of the current event in the list of events associated with the waiter
//  waiterEntry - list entry of the waiter to add
void UnixEvent::AddWaiter(UnixEventWaiter::ListEntry* waiterEntry)
{
    pthread_mutex_lock(&m_mutex);
    m_waiters.PushHead(waiterEntry);
    // If the event is set, signal the waiter right away
    if (m_state)
    {
        SignalWaiter(waiterEntry->GetEventIndex(), waiterEntry->GetWaiter());
    }
    pthread_mutex_unlock(&m_mutex);
}

// Remove waiter from the list of waiters waiting for the signal
// Parameters:
//  waiterEntry - list entry of the waiter to remove
void UnixEvent::RemoveWaiter(UnixEventWaiter::ListEntry* waiterEntry)
{
    pthread_mutex_lock(&m_mutex);
    bool found = m_waiters.RemoveFirst(waiterEntry);
    pthread_mutex_unlock(&m_mutex);

    ASSERT_MSG(found, "Attempt to remove waiter that was not added");
}

// Create new event
// Parameters:
//  manualReset  - true indicates a manual reset event, false an auto reset event
//  initialState - Initial state of the event. True means set, false not set.
UnixEvent::UnixEvent(bool manualReset, bool initialState)
: m_manualReset(manualReset),
  m_state(initialState)
{
    int st = pthread_mutex_init(&m_mutex, NULL);
    FATAL_ASSERT(st == 0, "Failed to initialize UnixEvent mutex");
}

UnixEvent::~UnixEvent()
{
    int st = pthread_mutex_destroy(&m_mutex);
    FATAL_ASSERT(st == 0, "Failed to destroy UnixEvent mutex");
}
