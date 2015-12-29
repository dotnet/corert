// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace System.Reflection
{
    /// <summary>
    /// Provides supporting functionality for DispatchProxy. Primarily this maintains a lookup
    /// table for dispatch proxy that maps proxy classes/interfaces => generated proxy instance
    /// classes, for each call to DispatchProxy.Create<ItfT, ProxyClassT>.
    /// </summary>
    /// <seealso cref="System.Reflection.DispatchProxy" />
    public static class DispatchProxyHelpers
    {
        private static DispatchProxyEntry[] s_entryTable;

        /// <summary>
        /// Mechanism for the toolchain to register a lookup table of DispatchProxyEntry
        /// records.
        /// </summary>
        /// <param name="entryTable">The toolchain-generated lookup table of DispatchProxyEntry
        /// records.</param>
        /// <seealso cref="System.Reflection.DispatchProxyEntry "/>
        public static void RegisterImplementations(DispatchProxyEntry[] entryTable)
        {
            s_entryTable = entryTable;
        }

        /// <summary>
        /// Finds the appropriate toolchain-generated proxy instance class for the given
        /// interface and proxy class.
        /// </summary>
        /// <param name="proxyClassTypeHandle">The proxy class the proxy instance class derives from/
        /// forwards its method calls to.</param>
        /// <param name="interfaceTypeHandle">The interface that the proxy instance class
        /// implements.</param>
        /// <returns>The type handle for the requested proxy instance class type.</returns>
        /// <exception cref="System.Reflection.DispatchProxyInstanceNotFoundException">
        /// Thrown when no generated proxy instance class exists for the requested proxy class
        /// and interface class types.
        /// </exception>
        public static RuntimeTypeHandle GetConcreteProxyType(RuntimeTypeHandle proxyClassTypeHandle,
                                                             RuntimeTypeHandle interfaceTypeHandle)
        {
            for (int i = 0; i < s_entryTable.Length; i++)
            {
                if ((s_entryTable[i].ProxyClassType.Equals(proxyClassTypeHandle)) &&
                    (s_entryTable[i].InterfaceType.Equals(interfaceTypeHandle)))
                {
                    return s_entryTable[i].ImplementationClassType;
                }
            }

            throw new DispatchProxyInstanceNotFoundException(
                "Could not find an DispatchProxy implementation class for the interface type '" +
                interfaceTypeHandle.ToString() + "' and the proxy class type '" +
                proxyClassTypeHandle.ToString() + "'."
            );
        }

        /// <summary>
        /// This is just a marker method to be picked up later during IL transformations. This is actually
        /// implemented by the "DispatchProxyIntrinsics" IL transformation, where the calling method is
        /// expected to have a methodimpl record indicating the implemented method. The IL transform will
        /// emit a ldtoken in that method.
        /// </summary>
        /// <exception cref="System.NotSupportedException">This method should never be called, so if
        /// someone tries to call it, we throw this exception instead.</exception>
        public static RuntimeMethodHandle GetCorrespondingInterfaceMethodFromMethodImpl()
        {
            throw new NotSupportedException();
        }
    }
}
