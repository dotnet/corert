// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.Runtime.InteropServices
{
    internal class CallbackContext : IDisposable
    {
        private Delegate _callbackMethod;
        private GCHandle _selfGCHandle;
        public virtual void Dispose()
        {
            _selfGCHandle.Free();
        }
        public Delegate CallbackMethod
        {
            get
            {
                return _callbackMethod;
            }
            set
            {
                //Mcg.Helpers.Assert((m_callbackMethod == null));
                _callbackMethod = value;
            }
        }
        public IntPtr GetContextHandle()
        {
            if ((_selfGCHandle.IsAllocated == false))
            {
                _selfGCHandle = GCHandle.Alloc(
                                    this,
                                    GCHandleType.Normal);
            }
            return GCHandle.ToIntPtr(_selfGCHandle);
        }
        public static object GetObjectFromContextHandle(IntPtr contextHandle)
        {
            return GCHandle.FromIntPtr(contextHandle).Target;
        }
    }
}
