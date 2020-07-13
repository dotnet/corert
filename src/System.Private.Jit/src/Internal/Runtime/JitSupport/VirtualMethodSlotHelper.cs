// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Internal.TypeSystem;
using Internal.Runtime.TypeLoader;

using ILCompiler.DependencyAnalysis;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler
{
    /// <summary>
    /// Jit specific version of virtual method slot resolution. Completely different from the compiler implementation
    /// as it is able to function in a partial metadata environment.
    /// </summary>
    internal static class VirtualMethodSlotHelper
    {
        /// <summary>
        /// Given a virtual method decl, return its VTable slot if the method is used on its containing type.
        /// Return -1 if the virtual method is not used.
        /// </summary>
        public static int GetVirtualMethodSlot(NodeFactory factory, MethodDesc method, TypeDesc implType)
        {
            Debug.Assert(method.IsVirtual);

            if (method.OwningType.IsInterface)
            {
                ushort slot;
                if (!LazyVTableResolver.TryGetInterfaceSlotNumberFromMethod(method, out slot))
                {
                    Environment.FailFast("Unable to get interface slot number for method");
                }
                return slot;
            }
            else
            {
                return LazyVTableResolver.VirtualMethodToSlotIndex(method);
            }
        }
    }
}
