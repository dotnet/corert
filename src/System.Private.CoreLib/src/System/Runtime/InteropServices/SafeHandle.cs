// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*============================================================
**
**
**
** A specially designed handle wrapper to ensure we never leak
** an OS handle.  The runtime treats this class specially during
** P/Invoke marshaling and finalization.  Users should write
** subclasses of SafeHandle for each distinct handle type.
**
** 
===========================================================*/

using System;
using System.Threading;

namespace System.Runtime.InteropServices
{
    /*
      Problems addressed by the SafeHandle class:
      1) Critical finalization - ensure we never leak OS resources in SQL.  Done
      without running truly arbitrary & unbounded amounts of managed code.
      2) Reduced graph promotion - during finalization, keep object graph small
      3) GC.KeepAlive behavior - P/Invoke vs. finalizer thread race (HandleRef)
      4) Elimination of security races w/ explicit calls to Close (HandleProtector)
      5) Enforcement of the above via the type system - Don't use IntPtr anymore.
      6) Allows the handle lifetime to be controlled externally via a boolean.

      Subclasses of SafeHandle will implement the ReleaseHandle abstract method
      used to execute any code required to free the handle. This method will be
      prepared as a constrained execution region at instance construction time
      (along with all the methods in its statically determinable call graph). This
      implies that we won't get any inconvenient jit allocation errors or rude
      thread abort interrupts while releasing the handle but the user must still
      write careful code to avoid injecting fault paths of their own (see the CER
      spec for more details). In particular, any sub-methods you call should be
      decorated with a reliability contract of the appropriate level. In most cases
      this should be:
      ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)
      Also, any P/Invoke methods should use the SuppressUnmanagedCodeSecurity
      attribute to avoid a runtime security check that can also inject failures
      (even if the check is guaranteed to pass).

      The GC will run ReleaseHandle methods after any normal finalizers have been
      run for objects that were collected at the same time. This ensures classes
      like FileStream can run a normal finalizer to flush out existing buffered
      data. This is key - it means adding this class to a class like FileStream does
      not alter our current semantics w.r.t. finalization today.

      Subclasses must also implement the IsInvalid property so that the
      infrastructure can tell when critical finalization is actually required.
      Again, this method is prepared ahead of time. It's envisioned that direct
      subclasses of SafeHandle will provide an IsInvalid implementation that suits
      the general type of handle they support (null is invalid, -1 is invalid etc.)
      and then these classes will be further derived for specific safe handle types.

      Most classes using SafeHandle should not provide a finalizer.  If they do
      need to do so (ie, for flushing out file buffers, needing to write some data
      back into memory, etc), then they can provide a finalizer that will be 
      guaranteed to run before the SafeHandle's critical finalizer.  

      Note that SafeHandle's ReleaseHandle is called from a constrained execution 
      region, and is eagerly prepared before we create your class.  This means you
      should only call methods with an appropriate reliability contract from your
      ReleaseHandle method.

      Subclasses are expected to be written as follows (note that
      SuppressUnmanagedCodeSecurity should always be used on any P/Invoke methods
      invoked as part of ReleaseHandle, in order to switch the security check from
      runtime to jit time and thus remove a possible failure path from the
      invocation of the method):

      internal sealed MySafeHandleSubclass : SafeHandle {
      // Called by P/Invoke when returning SafeHandles
      private MySafeHandleSubclass() : base(IntPtr.Zero, true)
      {
      }

      // If & only if you need to support user-supplied handles
      internal MySafeHandleSubclass(IntPtr preexistingHandle, bool ownsHandle) : base(IntPtr.Zero, ownsHandle)
      {
      SetHandle(preexistingHandle);
      }

      // Do not provide a finalizer - SafeHandle's critical finalizer will
      // call ReleaseHandle for you.

      public override bool IsInvalid {
      get { return handle == IntPtr.Zero; }
      }

      override protected bool ReleaseHandle()
      {
      return MyNativeMethods.CloseHandle(handle);
      }
      }

      Then elsewhere to create one of these SafeHandles, define a method
      with the following type of signature (CreateFile follows this model).
      Note that when returning a SafeHandle like this, P/Invoke will call your
      class's default constructor.  Also, you probably want to define CloseHandle
      somewhere, and remember to apply a reliability contract to it.

      [SuppressUnmanagedCodeSecurity]
      internal static class MyNativeMethods {
      [DllImport(Win32Native.CORE_HANDLE)]
      private static extern MySafeHandleSubclass CreateHandle(int someState);

      [DllImport(Win32Native.CORE_HANDLE, SetLastError=true), ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
      private static extern bool CloseHandle(IntPtr handle);
      }

      Drawbacks with this implementation:
      1) Requires some magic to run the critical finalizer.
    */

    public abstract class SafeHandle : IDisposable
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2111:PointersShouldNotBeVisible")]  
        protected IntPtr handle;        // PUBLICLY DOCUMENTED handle field

        private int _state;            // Combined ref count and closed/disposed flags (so we can atomically modify them).
        private bool _ownsHandle;       // Whether we can release this handle.

        // Bitmasks for the _state field above.
        private static class StateBits
        {
            public const int Closed = 0x00000001;
            public const int Disposed = 0x00000002;
            public const int RefCount = unchecked((int)0xfffffffc);
            public const int RefCountOne = 4;       // Amount to increment state field to yield a ref count increment of 1
        };

        // Creates a SafeHandle class.  Users must then set the Handle property.
        // To prevent the SafeHandle from being freed, write a subclass that
        // doesn't define a finalizer.
        protected SafeHandle(IntPtr invalidHandleValue, bool ownsHandle)
        {
            handle = invalidHandleValue;
            _state = StateBits.RefCountOne; // Ref count 1 and not closed or disposed.
            _ownsHandle = ownsHandle;

            if (!ownsHandle)
                GC.SuppressFinalize(this);
        }

        //
        // The handle cannot be closed until we are sure that no other objects might
        // be using it.  In the case of finalization, there may be other objects in
        // the finalization queue that still hold a reference to this SafeHandle.  
        // So we can't assume that just because our finalizer is running, no other
        // object will need to access this handle.
        //
        // The CLR solves this by having SafeHandle derive from CriticalFinalizerObject.
        // This ensures that SafeHandle's finalizer will run only after all "normal"
        // finalizers in the queue.  But MRT doesn't support CriticalFinalizerObject, or
        // any other explicit control of finalization order.
        //
        // For now, we'll hack this by not releasing the handle when our finalizer
        // is called.  Instead, we create a new DelayedFinalizer instance, whose
        // finalizer will release the handle.  Thus the handle won't be released in this
        // finalization cycle, but should be released in the next.
        //
        // This has the effect of delaying cleanup for much longer than would have 
        // happened on the CLR.  This also means that we may not close some handles
        // at shutdown, since there may not be another finalization cycle to run
        // the delayed finalizer.  If either of these end up being a problem, we should 
        // consider adding more control over finalization order to MRT (or, better, 
        // turning over control of finalization ordering to System.Private.CoreLib).
        //
        private class DelayedFinalizer
        {
            private SafeHandle _safeHandle;

            public DelayedFinalizer(SafeHandle safeHandle)
            {
                _safeHandle = safeHandle;
            }

            ~DelayedFinalizer()
            {
                _safeHandle.Dispose(false);
            }
        }

        ~SafeHandle()
        {
            new DelayedFinalizer(this);
        }

        // Keep the 'handle' variable named 'handle' to make sure it matches the surface area
        protected void SetHandle(IntPtr handle)
        {
            this.handle = handle;
        }

        // Used by Interop marshalling code
        internal void InitializeHandle(IntPtr _handle)
        {
            // The SafeHandle should be invalid to be able to initialize it
            System.Diagnostics.Debug.Assert(IsInvalid);

            handle = _handle;
        }

        // This method is necessary for getting an IntPtr out of a SafeHandle.
        // Used to tell whether a call to create the handle succeeded by comparing
        // the handle against a known invalid value, and for backwards 
        // compatibility to support the handle properties returning IntPtrs on
        // many of our Framework classes.
        // Note that this method is dangerous for two reasons:
        //  1) If the handle has been marked invalid with SetHandleasInvalid,
        //     DangerousGetHandle will still return the original handle value.
        //  2) The handle returned may be recycled at any point. At best this means
        //     the handle might stop working suddenly. At worst, if the handle or
        //     the resource the handle represents is exposed to untrusted code in
        //     any way, this can lead to a handle recycling security attack (i.e. an
        //     untrusted caller can query data on the handle you've just returned
        //     and get back information for an entirely unrelated resource).
        public IntPtr DangerousGetHandle()
        {
            return handle;
        }

        public bool IsClosed
        {
            get { return (_state & StateBits.Closed) == StateBits.Closed; }
        }

        public abstract bool IsInvalid
        {
            get;
        }

        internal void Close()
        {
            Dispose(true);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            InternalRelease(true);
            GC.SuppressFinalize(this);
        }

        public void SetHandleAsInvalid()
        {
            int oldState, newState;
            do
            {
                oldState = _state;

                if ((oldState & StateBits.Closed) != 0)
                    return;

                newState = oldState | StateBits.Closed;
            } while (Interlocked.CompareExchange(ref _state, newState, oldState) != oldState);
        }

        // Implement this abstract method in your derived class to specify how to
        // free the handle. Be careful not write any code that's subject to faults
        // in this method (the runtime will prepare the infrastructure for you so
        // that no jit allocations etc. will occur, but don't allocate memory unless
        // you can deal with the failure and still free the handle).
        // The boolean returned should be true for success and false if the runtime
        // should fire a SafeHandleCriticalFailure MDA (CustomerDebugProbe) if that
        // MDA is enabled.
        protected abstract bool ReleaseHandle();

        // Add a reason why this handle should not be relinquished (i.e. have
        // ReleaseHandle called on it). This method has dangerous in the name since
        // it must always be used carefully (e.g. called within a CER) to avoid
        // leakage of the handle. It returns a boolean indicating whether the
        // increment was actually performed to make it easy for program logic to
        // back out in failure cases (i.e. is a call to DangerousRelease needed).
        // It is passed back via a ref parameter rather than as a direct return so
        // that callers need not worry about the atomicity of calling the routine
        // and assigning the return value to a variable (the variable should be
        // explicitly set to false prior to the call). The only failure cases are
        // when the method is interrupted prior to processing by a thread abort or
        // when the handle has already been (or is in the process of being)
        // released.
        public void DangerousAddRef(ref bool success)
        {
            DangerousAddRef_WithNoNullCheck();
            success = true;
        }

        // Partner to DangerousAddRef. This should always be successful when used in
        // a correct manner (i.e. matching a successful DangerousAddRef and called
        // from a region such as a CER where a thread abort cannot interrupt
        // processing). In the same way that unbalanced DangerousAddRef calls can
        // cause resource leakage, unbalanced DangerousRelease calls may cause
        // invalid handle states to become visible to other threads. This
        // constitutes a potential security hole (via handle recycling) as well as a
        // correctness problem -- so don't ever expose Dangerous* calls out to
        // untrusted code.
        public void DangerousRelease()
        {
            InternalRelease(false);
        }

        // Do not call this directly - only call through the extension method SafeHandleExtensions.DangerousAddRef.
        internal void DangerousAddRef_WithNoNullCheck()
        {
            // To prevent handle recycling security attacks we must enforce the
            // following invariant: we cannot successfully AddRef a handle on which
            // we've committed to the process of releasing.

            // We ensure this by never AddRef'ing a handle that is marked closed and
            // never marking a handle as closed while the ref count is non-zero. For
            // this to be thread safe we must perform inspection/updates of the two
            // values as a single atomic operation. We achieve this by storing them both
            // in a single aligned DWORD and modifying the entire state via interlocked
            // compare exchange operations.

            // Additionally we have to deal with the problem of the Dispose operation.
            // We must assume that this operation is directly exposed to untrusted
            // callers and that malicious callers will try and use what is basically a
            // Release call to decrement the ref count to zero and free the handle while
            // it's still in use (the other way a handle recycling attack can be
            // mounted). We combat this by allowing only one Dispose to operate against
            // a given safe handle (which balances the creation operation given that
            // Dispose suppresses finalization). We record the fact that a Dispose has
            // been requested in the same state field as the ref count and closed state.

            // So the state field ends up looking like this:
            //
            //  31                                                        2  1   0
            // +-----------------------------------------------------------+---+---+
            // |                           Ref count                       | D | C |
            // +-----------------------------------------------------------+---+---+
            // 
            // Where D = 1 means a Dispose has been performed and C = 1 means the
            // underlying handle has (or will be shortly) released.


            // Might have to perform the following steps multiple times due to
            // interference from other AddRef's and Release's.
            int oldState, newState;
            do
            {
                // First step is to read the current handle state. We use this as a
                // basis to decide whether an AddRef is legal and, if so, to propose an
                // update predicated on the initial state (a conditional write).
                oldState = _state;

                // Check for closed state.
                if ((oldState & StateBits.Closed) != 0)
                {
                    throw new ObjectDisposedException("SafeHandle");
                }

                // Not closed, let's propose an update (to the ref count, just add
                // StateBits.RefCountOne to the state to effectively add 1 to the ref count).
                // Continue doing this until the update succeeds (because nobody
                // modifies the state field between the read and write operations) or
                // the state moves to closed.
                newState = oldState + StateBits.RefCountOne;
            } while (Interlocked.CompareExchange(ref _state, newState, oldState) != oldState);
            // If we got here we managed to update the ref count while the state
            // remained non closed. So we're done.
        }

        private void InternalRelease(bool fDispose)
        {
            // See AddRef above for the design of the synchronization here. Basically we
            // will try to decrement the current ref count and, if that would take us to
            // zero refs, set the closed state on the handle as well.
            bool fPerformRelease = false;

            // Might have to perform the following steps multiple times due to
            // interference from other AddRef's and Release's.
            int oldState, newState;
            do
            {
                // First step is to read the current handle state. We use this cached
                // value to predicate any modification we might decide to make to the
                // state).
                oldState = _state;

                // If this is a Dispose operation we have additional requirements (to
                // ensure that Dispose happens at most once as the comments in AddRef
                // detail). We must check that the dispose bit is not set in the old
                // state and, in the case of successful state update, leave the disposed
                // bit set. Silently do nothing if Dispose has already been called
                // (because we advertise that as a semantic of Dispose).
                if (fDispose && ((oldState & StateBits.Disposed) != 0))
                    return;

                // We should never see a ref count of zero (that would imply we have
                // unbalanced AddRef and Releases). (We might see a closed state before
                // hitting zero though -- that can happen if SetHandleAsInvalid is
                // used).
                if ((oldState & StateBits.RefCount) == 0)
                {
                    throw new ObjectDisposedException("SafeHandle");
                }

                // If we're proposing a decrement to zero and the handle is not closed
                // and we own the handle then we need to release the handle upon a
                // successful state update.
                fPerformRelease = ((oldState & (StateBits.RefCount | StateBits.Closed)) == StateBits.RefCountOne);
                fPerformRelease &= _ownsHandle;

                // If so we need to check whether the handle is currently invalid by
                // asking the SafeHandle subclass. We must do this *before*
                // transitioning the handle to closed, however, since setting the closed
                // state will cause IsInvalid to always return true.
                if (fPerformRelease && IsInvalid)
                    fPerformRelease = false;

                // Attempt the update to the new state, fail and retry if the initial
                // state has been modified in the meantime. Decrement the ref count by
                // substracting StateBits.RefCountOne from the state then OR in the bits for
                // Dispose (if that's the reason for the Release) and closed (if the
                // initial ref count was 1).
                newState = (oldState - StateBits.RefCountOne) |
                    ((oldState & StateBits.RefCount) == StateBits.RefCountOne ? StateBits.Closed : 0) |
                    (fDispose ? StateBits.Disposed : 0);
            } while (Interlocked.CompareExchange(ref _state, newState, oldState) != oldState);

            // If we get here we successfully decremented the ref count. Additonally we
            // may have decremented it to zero and set the handle state as closed. In
            // this case (providng we own the handle) we will call the ReleaseHandle
            // method on the SafeHandle subclass.
            if (fPerformRelease)
                ReleaseHandle();
        }
    }

    internal static class SafeHandleExtensions
    {
        public static void DangerousAddRef(this SafeHandle safeHandle)
        {
            // This check provides rough compatibility with the desktop code (this code's desktop counterpart is AcquireSafeHandleFromWaitHandle() inside clr.dll)
            // which throws ObjectDisposed if someone passes an uninitialized WaitHandle into one of the Wait apis. We use an extension method
            // because otherwise, the "null this" would trigger a NullReferenceException before we ever get to this check.
            if (safeHandle == null)
                throw new ObjectDisposedException(SR.ObjectDisposed_Generic);
            safeHandle.DangerousAddRef_WithNoNullCheck();
        }
    }
}
