// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Security;
using System.Runtime.CompilerServices;
using System.Threading;
using Internal.Runtime.Augments;

namespace System.Runtime.InteropServices.WindowsRuntime
{
#if  ENABLE_WINRT
    // Helper functions to manually marshal data between .NET and WinRT
    public static class WindowsRuntimeMarshal
    {
        // Add an event handler to a Windows Runtime style event, such that it can be removed via a delegate
        // lookup at a later time.  This method adds the handler to the add method using the supplied
        // delegate.  It then stores the corresponding token in a dictionary for easy access by RemoveEventHandler
        // later.  Note that the dictionary is indexed by the remove method that will be used for RemoveEventHandler
        // so the removeMethod given here must match the remove method supplied there exactly.
        public static void AddEventHandler<T>(Func<T, EventRegistrationToken> addMethod,
                                              Action<EventRegistrationToken> removeMethod,
                                              T handler)
        {
            if (addMethod == null)
                throw new ArgumentNullException(nameof(addMethod));
            if (removeMethod == null)
                throw new ArgumentNullException(nameof(removeMethod));

            // Managed code allows adding a null event handler, the effect is a no-op.  To match this behavior
            // for WinRT events, we simply ignore attempts to add null.
            if (handler == null)
            {
                return;
            }

            // Delegate to managed event registration implementation or native event registration implementation
            // They have completely different implementation because native side has its own unique problem to solve -
            // Managed events are implemented using ConditionalWeakTable which is based on the weak reference of the event itself.
            // Since the managed event will be alive till the event is used. This is OK.

            // On the other hand native and static events can't follow the same model as managed object. Since the native event might be alive but the managed __ComObject might
            // die, a different __ComObject instance might reference the same native event. Or same __ComObject instance might mean a different native event.
            // and hence they both have different implementations.
#if !RHTESTCL
            object target = removeMethod.Target;
            if (target == null || target is __ComObject)
                NativeOrStaticEventRegistrationImpl.AddEventHandler<T>(addMethod, removeMethod, handler);
            else
#endif
            ManagedEventRegistrationImpl.AddEventHandler<T>(addMethod, removeMethod, handler);
        }

        // Remove the delegate handler from the Windows Runtime style event registration by looking for
        // its token, previously stored via AddEventHandler<T>
        public static void RemoveEventHandler<T>(Action<EventRegistrationToken> removeMethod, T handler)
        {
            if (removeMethod == null)
                throw new ArgumentNullException(nameof(removeMethod));

            // Managed code allows removing a null event handler, the effect is a no-op.  To match this behavior
            // for WinRT events, we simply ignore attempts to remove null.
            if (handler == null)
            {
                return;
            }

            // Delegate to managed event registration implementation or native event registration implementation
            // They have completely different implementation because native side has its own unique problem to solve -
            // there could be more than one RCW for the same COM object
            // it would be more confusing and less-performant if we were to merge them together
#if !RHTESTCL
            object target = removeMethod.Target;
            if (target == null || target is __ComObject)
                NativeOrStaticEventRegistrationImpl.RemoveEventHandler<T>(removeMethod, handler);
            else
#endif
            ManagedEventRegistrationImpl.RemoveEventHandler<T>(removeMethod, handler);
        }

        public static void RemoveAllEventHandlers(Action<EventRegistrationToken> removeMethod)
        {
            if (removeMethod == null)
                throw new ArgumentNullException(nameof(removeMethod));

            // Delegate to managed event registration implementation or native event registration implementation
            // They have completely different implementation because native side has its own unique problem to solve -
            // there could be more than one RCW for the same COM object
            // it would be more confusing and less-performant if we were to merge them together
#if !RHTESTCL
            object target = removeMethod.Target;
            if (target == null || target is __ComObject)
                NativeOrStaticEventRegistrationImpl.RemoveAllEventHandlers(removeMethod);
            else
#endif
            ManagedEventRegistrationImpl.RemoveAllEventHandlers(removeMethod);
        }

#if !RHTESTCL
        // Returns the total cache size
        // Used by test only to verify we don't leak event cache
        internal static int GetRegistrationTokenCacheSize()
        {
            int count = 0;

            if (ManagedEventRegistrationImpl.s_eventRegistrations != null)
            {
                try
                {
                    ManagedEventRegistrationImpl.s_eventRegistrationsLock.Acquire();

                    foreach (var item in ManagedEventRegistrationImpl.s_eventRegistrations)
                        count++;
                }
                finally
                {
                    ManagedEventRegistrationImpl.s_eventRegistrationsLock.Release();
                }
            }

            if (NativeOrStaticEventRegistrationImpl.s_eventRegistrations != null)
            {
                try
                {
                    NativeOrStaticEventRegistrationImpl.s_eventRegistrationsLock.Acquire();

                    count += NativeOrStaticEventRegistrationImpl.s_eventRegistrations.Count;
                }
                finally
                {
                    NativeOrStaticEventRegistrationImpl.s_eventRegistrationsLock.Release();
                }
            }

            return count;
        }
#endif

        //
        // Optimized version of List of EventRegistrationToken
        // It is made a struct to reduce overhead
        //
        internal struct EventRegistrationTokenList
        {
            private EventRegistrationToken firstToken;     // Optimization for common case where there is only one token
            private System.Collections.Generic.Internal.List<EventRegistrationToken> restTokens;     // Rest of the tokens

            internal EventRegistrationTokenList(EventRegistrationToken token)
            {
                firstToken = token;
                restTokens = null;
            }

            internal EventRegistrationTokenList(EventRegistrationTokenList list)
            {
                firstToken = list.firstToken;
                restTokens = list.restTokens;
            }

            // Push a new token into this list
            // Returns true if you need to copy back this list into the dictionary (so that you
            // don't lose change outside the dictionary). false otherwise.
            public bool Push(EventRegistrationToken token)
            {
                bool needCopy = false;

                if (restTokens == null)
                {
                    restTokens = new System.Collections.Generic.Internal.List<EventRegistrationToken>();
                    needCopy = true;
                }

                restTokens.Add(token);

                return needCopy;
            }

            // Pops the last token
            // Returns false if no more tokens left, true otherwise
            public bool Pop(out EventRegistrationToken token)
            {
                // Only 1 token in this list and we just removed the last token
                if (restTokens == null || restTokens.Count == 0)
                {
                    token = firstToken;
                    return false;
                }

                int last = restTokens.Count - 1;
                token = restTokens[last];
                restTokens.RemoveAt(last);

                return true;
            }

            public void CopyTo(System.Collections.Generic.Internal.List<EventRegistrationToken> tokens)
            {
                tokens.Add(firstToken);

                if (restTokens != null)
                {
                    for (int i = 0; i < restTokens.Count; i++)
                    {
                        tokens.Add(restTokens[i]);
                    }
                }
            }
        }

        //
        // Event registration support for managed objects events & static events
        //
        internal static class ManagedEventRegistrationImpl
        {
            // Mappings of delegates registered for events -> their registration tokens.
            // These mappings are stored indexed by the remove method which can be used to undo the registrations.
            //
            // The full structure of this table is:
            //   object the event is being registered on ->
            //      Table [RemoveMethod] ->
            //        Table [Handler] -> Token
            //
            // Note: There are a couple of optimizations I didn't do here because they don't make sense for managed events:
            // 1.  Flatten the event cache (see EventCacheKey in native WinRT event implementation below)
            //
            //     This is because managed events use ConditionalWeakTable to hold Objects->(Event->(Handler->Tokens)),
            //     and when object goes away everything else will be nicely cleaned up. If I flatten it like native WinRT events,
            //     I'll have to use Dictionary (as ConditionalWeakTable won't work - nobody will hold the new key alive anymore)
            //     instead, and that means I'll have to add more code from native WinRT events into managed WinRT event to support
            //     self-cleanup in the finalization, as well as reader/writer lock to protect against races in the finalization,
            //     which adds a lot more complexity and doesn't really worth it.
            //
            // 2.  Use conditionalWeakTable to hold Handler->Tokens.
            //
            //     The reason is very simple - managed object use dictionary (see EventRegistrationTokenTable) to hold delegates alive.
            //     If the delegates aren't alive, it means either they have been unsubscribed, or the object itself is gone,
            //     and in either case, they've been already taken care of.
            //
            internal static
                ConditionalWeakTable<object, System.Collections.Generic.Internal.Dictionary<IntPtr, System.Collections.Generic.Internal.Dictionary<object, EventRegistrationTokenList>>> s_eventRegistrations =
                    new ConditionalWeakTable<object, System.Collections.Generic.Internal.Dictionary<IntPtr, System.Collections.Generic.Internal.Dictionary<object, EventRegistrationTokenList>>>();

            internal static Lock s_eventRegistrationsLock = new Lock();

            internal static void AddEventHandler<T>(Func<T, EventRegistrationToken> addMethod,
                                                  Action<EventRegistrationToken> removeMethod,
                                                  T handler)
            {
                Debug.Assert(addMethod != null);
                Debug.Assert(removeMethod != null);

                // Add the method, and make a note of the token -> delegate mapping.
                object instance = removeMethod.Target;

#if !RHTESTCL
                Debug.Assert(instance != null && !(instance is __ComObject));
#endif
                System.Collections.Generic.Internal.Dictionary<object, EventRegistrationTokenList> registrationTokens = GetEventRegistrationTokenTable(instance, removeMethod);

                EventRegistrationToken token = addMethod(handler);

                try
                {
                    registrationTokens.LockAcquire();

                    EventRegistrationTokenList tokens;

                    if (!registrationTokens.TryGetValue(handler, out tokens))
                    {
                        tokens = new EventRegistrationTokenList(token);
                        registrationTokens[handler] = tokens;
                    }
                    else
                    {
                        bool needCopy = tokens.Push(token);

                        // You need to copy back this list into the dictionary (so that you don't lose change outside dictionary)
                        if (needCopy)
                            registrationTokens[handler] = tokens;
                    }
#if false
                    BCLDebug.Log("INTEROP", "[WinRT_Eventing] Event subscribed for managed instance = " + instance + ", handler = " + handler + "\n");
#endif
                }
                finally
                {
                    registrationTokens.LockRelease();
                }
            }

            // Get the event registration token table for an event.  These are indexed by the remove method of the event.
            private static System.Collections.Generic.Internal.Dictionary<object, EventRegistrationTokenList> GetEventRegistrationTokenTable(object instance, Action<EventRegistrationToken> removeMethod)
            {
                Debug.Assert(instance != null);
                Debug.Assert(removeMethod != null);
                Debug.Assert(s_eventRegistrations != null);

                try
                {
                    s_eventRegistrationsLock.Acquire();

                    System.Collections.Generic.Internal.Dictionary<IntPtr, System.Collections.Generic.Internal.Dictionary<object, EventRegistrationTokenList>> instanceMap = null;

                    if (!s_eventRegistrations.TryGetValue(instance, out instanceMap))
                    {
                        instanceMap = new System.Collections.Generic.Internal.Dictionary<IntPtr, System.Collections.Generic.Internal.Dictionary<object, EventRegistrationTokenList>>();
                        s_eventRegistrations.Add(instance, instanceMap);
                    }

                    System.Collections.Generic.Internal.Dictionary<object, EventRegistrationTokenList> tokens = null;

                    // Because this code already is tied to a specific instance, the type handle associated with the
                    // delegate is not needed.
                    RuntimeTypeHandle thDummy;

                    if (!instanceMap.TryGetValue(removeMethod.GetFunctionPointer(out thDummy), out tokens))
                    {
                        tokens = new System.Collections.Generic.Internal.Dictionary<object, EventRegistrationTokenList>(true);
                        instanceMap.Add(removeMethod.GetFunctionPointer(out thDummy), tokens);
                    }

                    return tokens;
                }
                finally
                {
                    s_eventRegistrationsLock.Release();
                }
            }

            internal static void RemoveEventHandler<T>(Action<EventRegistrationToken> removeMethod, T handler)
            {
                Debug.Assert(removeMethod != null);

                object instance = removeMethod.Target;

                //
                // Temporary static event support - this is bad for a couple of reasons:
                // 1. This will leak the event delegates. Our real implementation fixes that
                // 2. We need the type itself, but we don't have delegate.Method.DeclaringType (
                // but I don't know what is the best replacement). Perhaps this isn't too bad
                // 3. Unsubscription doesn't work due to ConditionalWeakTable work on reference equality.
                // I can fix this but I figured it is easier to keep this broken so that we know we'll fix
                // this (rather than using the slower value equality version which we might forget to fix
                // later
                // @TODO - Remove this and replace with real static support (that was #ifdef-ed out)
                //
                if (instance == null)
                {
                    // Because this code only operates for delegates to static methods, the output typehandle of GetFunctionPointer is not used
                    RuntimeTypeHandle thDummy;
                    instance = removeMethod.GetFunctionPointer(out thDummy);
                }

                System.Collections.Generic.Internal.Dictionary<object, EventRegistrationTokenList> registrationTokens = GetEventRegistrationTokenTable(instance, removeMethod);
                EventRegistrationToken token;

                try
                {
                    registrationTokens.LockAcquire();

                    EventRegistrationTokenList tokens;

                    // Failure to find a registration for a token is not an error - it's simply a no-op.
                    if (!registrationTokens.TryGetValue(handler, out tokens))
                    {
#if false
                        BCLDebug.Log("INTEROP", "[WinRT_Eventing] no registrationTokens found for instance=" + instance + ", handler= " + handler + "\n");
#endif

                        return;
                    }

                    // Select a registration token to unregister
                    // We don't care which one but I'm returning the last registered token to be consistent
                    // with native event registration implementation
                    bool moreItems = tokens.Pop(out token);
                    if (!moreItems)
                    {
                        // Remove it from cache if this list become empty
                        // This must be done because EventRegistrationTokenList now becomes invalid
                        // (mostly because there is no safe default value for EventRegistrationToken to express 'no token')
                        // NOTE: We should try to remove registrationTokens itself from cache if it is empty, otherwise
                        // we could run into a race condition where one thread removes it from cache and another thread adds
                        // into the empty registrationToken table
                        registrationTokens.Remove(handler);
                    }
                }
                finally
                {
                    registrationTokens.LockRelease();
                }

                removeMethod(token);
#if false
                BCLDebug.Log("INTEROP", "[WinRT_Eventing] Event unsubscribed for managed instance = " + instance + ", handler = " + handler + ", token = " + token.m_value + "\n");
#endif
            }

            internal static void RemoveAllEventHandlers(Action<EventRegistrationToken> removeMethod)
            {
                Debug.Assert(removeMethod != null);

                object instance = removeMethod.Target;
                System.Collections.Generic.Internal.Dictionary<object, EventRegistrationTokenList> registrationTokens = GetEventRegistrationTokenTable(instance, removeMethod);

                System.Collections.Generic.Internal.List<EventRegistrationToken> tokensToRemove = new System.Collections.Generic.Internal.List<EventRegistrationToken>();

                try
                {
                    registrationTokens.LockAcquire();

                    // Copy all tokens to tokensToRemove array which later we'll call removeMethod on
                    // outside this lock
                    foreach (EventRegistrationTokenList tokens in registrationTokens.Values)
                    {
                        tokens.CopyTo(tokensToRemove);
                    }

                    // Clear the dictionary - at this point all event handlers are no longer in the cache
                    // but they are not removed yet
                    registrationTokens.Clear();
#if false
                    BCLDebug.Log("INTEROP", "[WinRT_Eventing] Cache cleared for managed instance = " + instance + "\n");
#endif
                }
                finally
                {
                    registrationTokens.LockRelease();
                }

                //
                // Remove all handlers outside the lock
                //
#if false
                BCLDebug.Log("INTEROP", "[WinRT_Eventing] Start removing all events for instance = " + instance + "\n");
#endif
                CallRemoveMethods(removeMethod, tokensToRemove);
#if false
                BCLDebug.Log("INTEROP", "[WinRT_Eventing] Finished removing all events for instance = " + instance + "\n");
#endif
            }
        }

#if !RHTESTCL
        //
        // WinRT event registration implementation code
        //
        internal static class NativeOrStaticEventRegistrationImpl
        {
            //
            // Key = (target object, event)
            // We use a key of object+event to save an extra dictionary
            //
            internal struct EventCacheKey
            {
                internal object target;
                internal object method;

                public override string ToString()
                {
                    return "(" + target + ", " + method + ")";
                }
            }

            internal class EventCacheKeyEqualityComparer : IEqualityComparer<EventCacheKey>
            {
                public bool Equals(EventCacheKey lhs, EventCacheKey rhs)
                {
                    return (Object.Equals(lhs.target, rhs.target) && Object.Equals(lhs.method, rhs.method));
                }

                public int GetHashCode(EventCacheKey key)
                {
                    return key.target.GetHashCode() ^ key.method.GetHashCode();
                }
            }

            //
            // EventRegistrationTokenListWithCount
            //
            // A list of EventRegistrationTokens that maintains a count
            //
            // The reason this needs to be a separate class is that we need a finalizer for this class
            // If the delegate is collected, it will take this list away with it (due to dependent handles),
            // and we need to remove the PerInstancEntry from cache
            // See ~EventRegistrationTokenListWithCount for more details
            //
            internal class EventRegistrationTokenListWithCount
            {
                private TokenListCount _tokenListCount;
                EventRegistrationTokenList _tokenList;

                internal EventRegistrationTokenListWithCount(TokenListCount tokenListCount, EventRegistrationToken token)
                {
                    _tokenListCount = tokenListCount;
                    _tokenListCount.Inc();

                    _tokenList = new EventRegistrationTokenList(token);
                }

                ~EventRegistrationTokenListWithCount()
                {
                    // Decrement token list count
                    // This is need to correctly keep trace of number of tokens for EventCacheKey
                    // and remove it from cache when the token count drop to 0
                    // we don't need to take locks for decrement the count - we only need to take a global
                    // lock when we decide to destroy cache for the IUnknown */type instance
#if false
                    BCLDebug.Log("INTEROP", "[WinRT_Eventing] Finalizing EventRegistrationTokenList for " + _tokenListCount.Key + "\n");
#endif
                    _tokenListCount.Dec();
                }

                public void Push(EventRegistrationToken token)
                {
                    // Since EventRegistrationTokenListWithCount is a reference type, there is no need
                    // to copy back. Ignore the return value
                    _tokenList.Push(token);
                }

                public bool Pop(out EventRegistrationToken token)
                {
                    return _tokenList.Pop(out token);
                }

                public void CopyTo(System.Collections.Generic.Internal.List<EventRegistrationToken> tokens)
                {
                    _tokenList.CopyTo(tokens);
                }
            }

            //
            // Maintains the number of tokens for a particular EventCacheKey
            // TokenListCount is a class for two reasons:
            // 1. Efficient update in the Dictionary to avoid lookup twice to update the value
            // 2. Update token count without taking a global lock. Only takes a global lock when drop to 0
            //
            internal class TokenListCount
            {
                private int _count;
                private EventCacheKey _key;

                internal TokenListCount(EventCacheKey key)
                {
                    _key = key;
                }

                internal EventCacheKey Key
                {
                    get { return _key; }
                }

                internal void Inc()
                {
                    int newCount = Interlocked.Increment(ref _count);
#if false
                    BCLDebug.Log("INTEROP", "[WinRT_Eventing] Incremented TokenListCount for " + _key + ", Value = " + newCount + "\n");
#endif
                }

                internal void Dec()
                {
                    // Avoid racing with Add/Remove event entries into the cache
                    // You don't want this removing the key in the middle of a Add/Remove
                    s_eventCacheRWLock.EnterWriteLock();
                    try
                    {
                        int newCount = Interlocked.Decrement(ref _count);
#if false
                        BCLDebug.Log("INTEROP", "[WinRT_Eventing] Decremented TokenListCount for " + _key + ", Value = " + newCount + "\n");
#endif
                        if (newCount == 0)
                            CleanupCache();
                    }
                    finally
                    {
                        s_eventCacheRWLock.ExitWriteLock();
                    }
                }

                private void CleanupCache()
                {
                    // Time to destroy cache for this IUnknown */type instance
                    // because the total token list count has dropped to 0 and we don't have any events subscribed
                    Debug.Assert(s_eventRegistrations != null);
#if false
                    BCLDebug.Log("INTEROP", "[WinRT_Eventing] Removing " + _key + " from cache" + "\n");
#endif
                    s_eventRegistrations.Remove(_key);
#if false
                    BCLDebug.Log("INTEROP", "[WinRT_Eventing] s_eventRegistrations size = " + s_eventRegistrations.Count + "\n");
#endif
                }
            }

            internal class EventCacheEntry
            {
                // [Handler] -> Token
                internal ConditionalWeakTable<object, EventRegistrationTokenListWithCount> registrationTable;

                // Maintains current total count for the EventRegistrationTokenListWithCount for this event cache key
                internal TokenListCount tokenListCount;

                // Lock for registrationTable + tokenListCount, much faster than locking ConditionalWeakTable itself
                internal Lock _lock;

                internal void LockAcquire()
                {
                    _lock.Acquire();
                }

                internal void LockRelease()
                {
                    _lock.Release();
                }
            }

            // Mappings of delegates registered for events -> their registration tokens.
            // These mappings are stored indexed by the remove method which can be used to undo the registrations.
            //
            // The full structure of this table is:
            //   EventCacheKey (instanceKey, eventMethod) -> EventCacheEntry (Handler->tokens)
            //
            // A InstanceKey is the IUnknown * or static type instance
            //
            // Couple of things to note:
            // 1. We need to use IUnknown* because we want to be able to unscribe to the event for another RCW
            // based on the same COM object. For example:
            //    m_canvas.GetAt(0).Event += Func;
            //    m_canvas.GetAt(0).Event -= Func;  // GetAt(0) might create a new RCW
            //
            // 2. Handler->Token is a ConditionalWeakTable because we don't want to keep the delegate alive
            // and we want EventRegistrationTokenListWithCount to be finalized after the delegate is no longer alive
            // 3. It is possible another COM object is created at the same address
            // before the entry in cache is destroyed. More specifically,
            //   a. The same delegate is being unsubscribed. In this case we'll give them a
            //   stale token - unlikely to be a problem
            //   b. The same delegate is subscribed then unsubscribed. We need to make sure give
            //   them the latest token in this case. This is guaranteed by always giving the last token and always use equality to
            //   add/remove event handlers
            internal static System.Collections.Generic.Internal.Dictionary<EventCacheKey, EventCacheEntry> s_eventRegistrations =
                new System.Collections.Generic.Internal.Dictionary<EventCacheKey, EventCacheEntry>(new EventCacheKeyEqualityComparer());

            internal static Lock s_eventRegistrationsLock = new Lock();

            // Prevent add/remove handler code to run at the same with with cache cleanup code
            private static ReaderWriterLockSlim s_eventCacheRWLock = new ReaderWriterLockSlim();

            private static Object s_dummyStaticEventKey = new Object();
            // Get InstanceKey to use in the cache
            private static object GetInstanceKey(Action<EventRegistrationToken> removeMethod)
            {
                object target = removeMethod.Target;
                Debug.Assert(target == null || target is __ComObject, "Must be an RCW");

                if (target == null)
                {
                    // In .NET Native there is no good way to go from the static event to the declaring type, the instanceKey used for
                    // static events in desktop. Since the declaring type is only a way to organize the list of static events, we have
                    // chosen to use the dummyObject instead here.It flattens the hierarchy of static events but does not impact the functionality.
                    return s_dummyStaticEventKey;
                }

                // Need the "Raw" IUnknown pointer for the RCW that is not bound to the current context
                __ComObject comObject = target as __ComObject;
                return (object)comObject.BaseIUnknown_UnsafeNoAddRef;
            }

            private static object FindEquivalentKeyUnsafe(ConditionalWeakTable<object, EventRegistrationTokenListWithCount> registrationTable, object handler, out EventRegistrationTokenListWithCount tokens)
            {
                foreach (KeyValuePair<object, EventRegistrationTokenListWithCount> item in registrationTable)
                {
                    if (Object.Equals(item.Key, handler))
                    {
                        tokens = item.Value;
                        return item.Key;
                    }
                }
                tokens = null;
                return null;
            }

            internal static void AddEventHandler<T>(Func<T, EventRegistrationToken> addMethod,
                                                  Action<EventRegistrationToken> removeMethod,
                                                  T handler)
            {
                // The instanceKey will be IUnknown * of the target object
                object instanceKey = GetInstanceKey(removeMethod);

                // Call addMethod outside of RW lock
                // At this point we don't need to worry about race conditions and we can avoid deadlocks
                // if addMethod waits on finalizer thread
                // If we later throw we need to remove the method
                EventRegistrationToken token = addMethod(handler);

                bool tokenAdded = false;

                try
                {
                    EventRegistrationTokenListWithCount tokens;

                    //
                    // The whole add/remove code has to be protected by a reader/writer lock
                    // Add/Remove cannot run at the same time with cache cleanup but Add/Remove can run at the same time
                    //
                    s_eventCacheRWLock.EnterReadLock();
                    try
                    {
                        // Add the method, and make a note of the delegate -> token mapping.
                        EventCacheEntry registrationTokens = GetOrCreateEventRegistrationTokenTable(instanceKey, removeMethod);

                        try
                        {
                            registrationTokens.LockAcquire();

                            //
                            // We need to find the key that equals to this handler
                            // Suppose we have 3 handlers A, B, C that are equal (refer to the same object and method),
                            // the first handler (let's say A) will be used as the key and holds all the tokens.
                            // We don't need to hold onto B and C, because the COM object itself will keep them alive,
                            // and they won't die anyway unless the COM object dies or they get unsubscribed.
                            // It may appear that it is fine to hold A, B, C, and add them and their corresponding tokens
                            // into registrationTokens table. However, this is very dangerous, because this COM object
                            // may die, but A, B, C might not get collected yet, and another COM object comes into life
                            // with the same IUnknown address, and we subscribe event B. In this case, the right token
                            // will be added into B's token list, but once we unsubscribe B, we might end up removing
                            // the last token in C, and that may lead to crash.
                            //
                            object key = FindEquivalentKeyUnsafe(registrationTokens.registrationTable, handler, out tokens);

                            if (key == null)
                            {
                                tokens = new EventRegistrationTokenListWithCount(registrationTokens.tokenListCount, token);
                                registrationTokens.registrationTable.Add(handler, tokens);
                            }
                            else
                            {
                                tokens.Push(token);
                            }

                            tokenAdded = true;
                        }
                        finally
                        {
                            registrationTokens.LockRelease();
                        }
                    }
                    finally
                    {
                        s_eventCacheRWLock.ExitReadLock();
                    }
#if false
                    BCLDebug.Log("INTEROP", "[WinRT_Eventing] Event subscribed for instance = " + instanceKey + ", handler = " + handler + "\n");
#endif
                }
                catch (Exception)
                {
                    // If we've already added the token and go there, we don't need to "UNDO" anything
                    if (!tokenAdded)
                    {
                        // Otherwise, "Undo" addMethod if any exception occurs
                        // There is no need to cleanup our data structure as we haven't added the token yet
                        removeMethod(token);
                    }


                    throw;
                }
            }

            private static EventCacheEntry GetEventRegistrationTokenTableNoCreate(object instance, Action<EventRegistrationToken> removeMethod)
            {
                Debug.Assert(instance != null);
                Debug.Assert(removeMethod != null);

                return GetEventRegistrationTokenTableInternal(instance, removeMethod, /* createIfNotFound = */ false);
            }

            private static EventCacheEntry GetOrCreateEventRegistrationTokenTable(object instance, Action<EventRegistrationToken> removeMethod)
            {
                Debug.Assert(instance != null);
                Debug.Assert(removeMethod != null);

                return GetEventRegistrationTokenTableInternal(instance, removeMethod, /* createIfNotFound = */ true);
            }

            // Get the event registration token table for an event.  These are indexed by the remove method of the event.
            private static EventCacheEntry GetEventRegistrationTokenTableInternal(object instance, Action<EventRegistrationToken> removeMethod, bool createIfNotFound)
            {
                Debug.Assert(instance != null);
                Debug.Assert(removeMethod != null);
                Debug.Assert(s_eventRegistrations != null);

                EventCacheKey eventCacheKey;
                eventCacheKey.target = instance;
#if false
                eventCacheKey.method = removeMethod.Method;
#endif
                RuntimeTypeHandle thDummy;
                eventCacheKey.method = removeMethod.GetFunctionPointer(out thDummy);

                try
                {
                    s_eventRegistrationsLock.Acquire();

                    EventCacheEntry eventCacheEntry;

                    if (!s_eventRegistrations.TryGetValue(eventCacheKey, out eventCacheEntry))
                    {
                        if (!createIfNotFound)
                        {
                            // No need to create an entry in this case
                            return null;
                        }
#if false
                        BCLDebug.Log("INTEROP", "[WinRT_Eventing] Adding (" + instance + "," + removeMethod.Method + ") into cache" + "\n");
#endif
                        eventCacheEntry = new EventCacheEntry();
                        eventCacheEntry.registrationTable = new ConditionalWeakTable<object, EventRegistrationTokenListWithCount>();
                        eventCacheEntry.tokenListCount = new TokenListCount(eventCacheKey);
                        eventCacheEntry._lock = new Lock();

                        s_eventRegistrations.Add(eventCacheKey, eventCacheEntry);
                    }

                    return eventCacheEntry;
                }
                finally
                {
                    s_eventRegistrationsLock.Release();
                }
            }

            internal static void RemoveEventHandler<T>(Action<EventRegistrationToken> removeMethod, T handler)
            {
                object instanceKey = GetInstanceKey(removeMethod);

                EventRegistrationToken token;

                //
                // The whole add/remove code has to be protected by a reader/writer lock
                // Add/Remove cannot run at the same time with cache cleanup but Add/Remove can run at the same time
                //
                s_eventCacheRWLock.EnterReadLock();
                try
                {
                    EventCacheEntry registrationTokens = GetEventRegistrationTokenTableNoCreate(instanceKey, removeMethod);
                    if (registrationTokens == null)
                    {
                        // We have no information regarding this particular instance (IUnknown*/type) - just return
                        // This is necessary to avoid leaking empty dictionary/conditionalWeakTables for this instance
#if false
                        BCLDebug.Log("INTEROP", "[WinRT_Eventing] no registrationTokens found for instance=" + instanceKey + ", handler= " + handler + "\n");
#endif
                        return;
                    }

                    try
                    {
                        registrationTokens.LockAcquire();

                        EventRegistrationTokenListWithCount tokens;

                        // Note:
                        // When unsubscribing events, we allow subscribing the event using a different delegate
                        // (but with the same object/method), so we need to find the first delegate that matches
                        // and unsubscribe it
                        // It actually doesn't matter which delegate - as long as it matches
                        // Note that inside TryGetValueWithValueEquality we assumes that any delegate
                        // with the same value equality would have the same hash code
                        object key = FindEquivalentKeyUnsafe(registrationTokens.registrationTable, handler, out tokens);

                        Debug.Assert((key != null && tokens != null) || (key == null && tokens == null),
                                        "key and tokens must be both null or non-null");
                        if (tokens == null)
                        {
                            // Failure to find a registration for a token is not an error - it's simply a no-op.
#if false
                            BCLDebug.Log("INTEROP", "[WinRT_Eventing] no token list found for instance=" + instanceKey + ", handler= " + handler + "\n");
#endif
                            return;
                        }

                        // Select a registration token to unregister
                        // Note that we need to always get the last token just in case another COM object
                        // is created at the same address before the entry for the old one goes away.
                        // See comments above s_eventRegistrations for more details
                        bool moreItems = tokens.Pop(out token);

                        // If the last token is removed from token list, we need to remove it from the cache
                        // otherwise FindEquivalentKeyUnsafe may found this empty token list even though there could be other
                        // equivalent keys in there with non-0 token list
                        if (!moreItems)
                        {
                            // Remove it from (handler)->(tokens)
                            // NOTE: We should not check whether registrationTokens has 0 entries and remove it from the cache
                            // (just like managed event implementation), because this might race with the finalizer of
                            // EventRegistrationTokenList
                            registrationTokens.registrationTable.Remove(key);
                        }
#if false
                        BCLDebug.Log("INTEROP", "[WinRT_Eventing] Event unsubscribed for managed instance = " + instanceKey + ", handler = " + handler + ", token = " + token.m_value + "\n");
#endif
                    }
                    finally
                    {
                        registrationTokens.LockRelease();
                    }
                }
                finally
                {
                    s_eventCacheRWLock.ExitReadLock();
                }
                // Call removeMethod outside of RW lock
                // At this point we don't need to worry about race conditions and we can avoid deadlocks
                // if removeMethod waits on finalizer thread
                removeMethod(token);
            }

            internal static void RemoveAllEventHandlers(Action<EventRegistrationToken> removeMethod)
            {
                object instanceKey = GetInstanceKey(removeMethod);

                System.Collections.Generic.Internal.List<EventRegistrationToken> tokensToRemove = new System.Collections.Generic.Internal.List<EventRegistrationToken>();

                //
                // The whole add/remove code has to be protected by a reader/writer lock
                // Add/Remove cannot run at the same time with cache cleanup but Add/Remove can run at the same time
                //
                s_eventCacheRWLock.EnterReadLock();
                try
                {
                    EventCacheEntry registrationTokens = GetEventRegistrationTokenTableNoCreate(instanceKey, removeMethod);
                    if (registrationTokens == null)
                    {
                        // We have no information regarding this particular instance (IUnknown*/type) - just return
                        // This is necessary to avoid leaking empty dictionary/conditionalWeakTables for this instance
                        return;
                    }

                    try
                    {
                        registrationTokens.LockAcquire();

                        // Copy all tokens to tokensToRemove array which later we'll call removeMethod on
                        // outside this lock
                        foreach (KeyValuePair<object, EventRegistrationTokenListWithCount> item in registrationTokens.registrationTable)
                        {
                            item.Value.CopyTo(tokensToRemove);
                        }

                        // Clear the table - at this point all event handlers are no longer in the cache
                        // but they are not removed yet
                        registrationTokens.registrationTable.Clear();
#if false
                        BCLDebug.Log("INTEROP", "[WinRT_Eventing] Cache cleared for managed instance = " + instanceKey + "\n");
#endif
                    }
                    finally
                    {
                        registrationTokens.LockRelease();
                    }
                }
                finally
                {
                    s_eventCacheRWLock.ExitReadLock();
                }

                //
                // Remove all handlers outside the lock
                //
#if false
                BCLDebug.Log("INTEROP", "[WinRT_Eventing] Start removing all events for instance = " + instanceKey + "\n");
#endif
                CallRemoveMethods(removeMethod, tokensToRemove);
#if false
                BCLDebug.Log("INTEROP", "[WinRT_Eventing] Finished removing all events for instance = " + instanceKey + "\n");
#endif
            }
        }
#endif

        //
        // Call removeMethod on each token and aggregate all exceptions thrown from removeMethod into one in case of failure
        //
        internal static void CallRemoveMethods(Action<EventRegistrationToken> removeMethod, System.Collections.Generic.Internal.List<EventRegistrationToken> tokensToRemove)
        {
            System.Collections.Generic.Internal.List<Exception> exceptions = new System.Collections.Generic.Internal.List<Exception>();

            for (int i = 0; i < tokensToRemove.Count; i++)
            {
                try
                {
                    removeMethod(tokensToRemove[i]);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
#if false
                BCLDebug.Log("INTEROP", "[WinRT_Eventing] Event unsubscribed for token = " + token.m_value + "\n");
#endif
            }

            if (exceptions.Count > 0)
#if false
                throw new AggregateException(exceptions.ToArray());
#else
                throw exceptions[0];
#endif
        }

        public static IntPtr StringToHString(string s)
        {
            return McgMarshal.StringToHString(s).handle;
        }

        public static void FreeHString(IntPtr ptr)
        {
            McgMarshal.FreeHString(ptr);
        }

        public static string PtrToStringHString(IntPtr ptr)
        {
            return McgMarshal.HStringToString(ptr);
        }

        /// <summary>
        /// Returns the activation factory without using the cache. Avoiding cache behavior is important
        /// for app that use this API because they need to deal with crashing broker scenarios where cached
        /// factories would be stale (pointing to a bad proxy)
        /// </summary>
        public static IActivationFactory GetActivationFactory(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            __ComObject factory = FactoryCache.Get().GetActivationFactory(
                type.FullName,
                InternalTypes.IUnknown,
                skipCache: true);
            return (IActivationFactory) factory;
        }
    }
#endif
}
