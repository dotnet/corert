// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// Implementation of events for Unix
//

#ifndef __UNIXEVENT_H__
#define __UNIXEVENT_H__

class UnixEventWaiter;
class UnixEvent;

// Helper class to enable waiting for multiple events
class UnixEventWaiter
{
    friend class UnixEvent;

    // m_eventIndex is set to this value when there is no signalled event
    static const int NoEventIndex = -1;

    // Maximum number of events that a waiter can wait on without dynamically allocating
    // array of ListEntry
    static const int MaximumEmbeddedEvents = 1;

    // Entry of the linked list of waiters
    class ListEntry
    {
        friend struct DefaultSListTraits<ListEntry>;
        // Next entry
        ListEntry* m_pNext;
        // Waiter this entry belongs to
        UnixEventWaiter* m_waiter;

    public:
        ListEntry() = default;

        ListEntry(UnixEventWaiter *waiter)
        : m_pNext(NULL),
          m_waiter(waiter)
        {
        }

        // Get waiter to which the current list entry belongs
        UnixEventWaiter* GetWaiter()
        {
            return m_waiter;
        }

        // Get per-waiter index of the event 
        int GetEventIndex()
        {
            return this - m_waiter->m_waiterListEntries;
        }
    };

    // Condition variable used for the Wait
    pthread_cond_t m_condition;
    // Mutex used by the condition variable
    pthread_mutex_t m_mutex;
    // Index of the event that caused termination of the wait
    int m_eventIndex;
    // Set to true if the wait has timed out
    bool m_timedOutOrFailed; 

    // Entries used by m_waiters linked lists of all events registered with the waiter
    ListEntry* m_waiterListEntries;

    // Entries used by m_waiters linked lists when the number of events to wait on
    // is less than or equal to MaximumEmbeddedEvents. It is an optimization to
    // prevent dynamic allocation of the entries in the most common cases.
    ListEntry m_embeddedWaiterListEntries[MaximumEmbeddedEvents];

    // Number of events the waiter waits on
    uint32_t m_eventCount;

public:

    UnixEventWaiter(uint32_t eventCount);
    ~UnixEventWaiter();

    // Add event to the waiter
    // Parameters:
    //  index - Index of the event that the waiter reports when the event is signalled
    //  event - Event to wait on
    void AddEvent(int index, UnixEvent* event);

    // Remove event from the waiter
    // Parameters:
    //  index - Index of the event to remove
    //  event - Event to remove
    void RemoveEvent(int index, UnixEvent* event);

    // Signal the waiter that an attached event was set. It is called 
    // by the UnixEvent instances. 
    // Parameters:
    //  index - index of the event in the list of events managed by this waiter
    // Return:
    //  true if the signal has caused the wait to complete, false if the wait
    //  completion was already triggered by another signal.
    bool Signal(int index);

    // Wait for one of the events attached to this waiter. It completes when
    // either one of the events is set or the wait timeouts.
    // Parameters:
    //  milliseconds - wait timeout
    // Return:
    //  One of the following values:
    //      WAIT_OBJECT_0 + n - the wait completed due to the event with index n
    //      WAIT_TIMEOUT      - the wait timed out
    //      WAIT_FAILED       - the wait has failed due to some system related issue
    uint32_t Wait(uint32_t milliseconds);
};

// Implementation of events for Unix.
// An event can either be manual reset or auto reset. Manual reset event remains set
// after the call to the Set method until the Reset method is called.
// Auto reset event is automatically reset as soon as a waiting thread is released
// by that. And only one waiting thread is released by this event.
class UnixEvent
{
    friend class UnixEventWaiter;
        
    // Mutex to synchronize access to the m_waiters list
    pthread_mutex_t m_mutex;

    // List of waiters waiting for the event
    SList<UnixEventWaiter::ListEntry> m_waiters;

    // true if this event is a manual reset event, false if it is an auto reset event
    bool m_manualReset;
    // Current state of the event
    bool m_state;

    // Signal a waiter that the event was set
    // Parameters:
    //  index  - index of the current event in the list of events associated with the waiter
    //  waiter - the waiter to signal
    void SignalWaiter(int index, UnixEventWaiter* waiter);

    // Add waiter to the list of waiters waiting for the signal
    // Parameters:
    //  index  - index of the current event in the list of events associated with the waiter
    //  waiterEntry - list entry of the waiter to add
    void AddWaiter(UnixEventWaiter::ListEntry* waiterEntry);

    // Remove waiter from the list of waiters waiting for the signal
    // Parameters:
    //  waiterEntry - list entry of the waiter to remove
    void RemoveWaiter(UnixEventWaiter::ListEntry* waiterEntry);

public:

    // Create new event
    // Parameters:
    //  manualReset  - true indicates a manual reset event, false an auto reset event
    //  initialState - Initial state of the event. True means set, false not set.
    UnixEvent(bool manualReset, bool initialState);
    ~UnixEvent();

    // Set the event to signalled state
    void Set();
    // Reset the event state to non-signalled
    void Reset();
};

#endif // __UNIXEVENT_H__
