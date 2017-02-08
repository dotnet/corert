// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace System.Threading
{
    public sealed class RuntimeThread
    {
        public static RuntimeThread Create(ThreadStart start) { throw null; }
        public static RuntimeThread Create(ThreadStart start, int maxStackSize) { throw null; }
        public static RuntimeThread Create(ParameterizedThreadStart start) { throw null; }
        public static RuntimeThread Create(ParameterizedThreadStart start, int maxStackSize) { throw null; }
        public static RuntimeThread CurrentThread { get { throw null; } }
        public bool IsAlive { get { throw null; } }
        public bool IsBackground { get { throw null; } set { throw null; } }
        public bool IsThreadPoolThread { get { throw null; } }
        public int ManagedThreadId { get { throw null; } }
        public string Name { get { throw null; } set { throw null; } }
        public ThreadPriority Priority { get { throw null; } set { throw null; } }
        public ThreadState ThreadState { get { throw null; } }
        public ApartmentState GetApartmentState() { throw null; }
        public bool TrySetApartmentState(ApartmentState state) { throw null; }
        public void DisableComObjectEagerCleanup() { throw null; }
        public void Interrupt() { throw null; }
        public void Join() { throw null; }
        public bool Join(int millisecondsTimeout) { throw null; }
        public static void Sleep(int millisecondsTimeout) { throw null; }
        public static void SpinWait(int iterations) { throw null; }
        public static bool Yield() { throw null; }
        public void Start() { throw null; }
        public void Start(object parameter) { throw null; }
    }
}
