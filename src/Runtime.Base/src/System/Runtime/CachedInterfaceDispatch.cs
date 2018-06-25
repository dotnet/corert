// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime;
using System.Runtime.CompilerServices;

using Internal.Runtime;
using Internal.Runtime.CompilerServices;

namespace System.Runtime
{
    internal static unsafe class CachedInterfaceDispatch
    {
        [RuntimeExport("RhpCidResolve")]
        unsafe private static IntPtr RhpCidResolve(IntPtr callerTransitionBlockParam, IntPtr pCell)
        {
            IntPtr locationOfThisPointer = callerTransitionBlockParam + TransitionBlock.GetThisOffset();
            object pObject = Unsafe.As<IntPtr, object>(ref *(IntPtr*)locationOfThisPointer);
            IntPtr dispatchResolveTarget = RhpCidResolve_Worker(pObject, pCell);

            if (dispatchResolveTarget == InternalCalls.RhpGetCastableObjectDispatchHelper())
            {
                // Swap it out for the one which reads the TLS slot to put the cell in a consistent
                // location
                dispatchResolveTarget = InternalCalls.RhpGetCastableObjectDispatchHelper_TailCalled();
                InternalCalls.RhpSetTLSDispatchCell(pCell);
            }

            return dispatchResolveTarget;
        }

        private static IntPtr RhpCidResolve_Worker(object pObject, IntPtr pCell)
        {
            DispatchCellInfo cellInfo;

            InternalCalls.RhpGetDispatchCellInfo(pCell, out cellInfo);
            IntPtr pTargetCode = RhResolveDispatchWorker(pObject, (void*)pCell, ref cellInfo);
            if (pTargetCode != IntPtr.Zero)
            {
                return InternalCalls.RhpUpdateDispatchCellCache(pCell, pTargetCode, pObject.EEType, ref cellInfo);
            }

            // "Valid method implementation was not found."
            EH.FallbackFailFast(RhFailFastReason.InternalError, null);
            return IntPtr.Zero;
        }

        [RuntimeExport("RhpResolveInterfaceMethod")]
        private static IntPtr RhpResolveInterfaceMethod(object pObject, IntPtr pCell)
        {
            if (pObject == null)
            {
                // ProjectN Optimizer may perform code motion on dispatch such that it occurs independant of 
                // null check on "this" pointer. Allow for this case by returning back an invalid pointer.
                return IntPtr.Zero;
            }

            EEType* pInstanceType = pObject.EEType;

            // This method is used for the implementation of LOAD_VIRT_FUNCTION and in that case the mapping we want
            // may already be in the cache.
            IntPtr pTargetCode = InternalCalls.RhpSearchDispatchCellCache(pCell, pInstanceType);
            if (pTargetCode == IntPtr.Zero)
            {
                // Otherwise call the version of this method that knows how to resolve the method manually.
                pTargetCode = RhpCidResolve_Worker(pObject, pCell);
            }

            // Special case for CastableOjbects: we use a thunk for the call. The thunk deals with putting
            // the correct cell address value in the TLS variable used by castable objects dispatch.
            if (pTargetCode == InternalCalls.RhpGetCastableObjectDispatchHelper())
                pTargetCode = CastableObjectSupport.GetCastableObjectDispatchCellThunk(pInstanceType, pCell);

            return pTargetCode;
        }

        [RuntimeExport("RhResolveDispatch")]
        private static IntPtr RhResolveDispatch(object pObject, EETypePtr interfaceType, ushort slot)
        {
            DispatchCellInfo cellInfo = new DispatchCellInfo();
            cellInfo.CellType = DispatchCellType.InterfaceAndSlot;
            cellInfo.InterfaceType = interfaceType;
            cellInfo.InterfaceSlot = slot;

            return RhResolveDispatchWorker(pObject, null, ref cellInfo);
        }

        [RuntimeExport("RhResolveDispatchOnType")]
        private static IntPtr RhResolveDispatchOnType(EETypePtr instanceType, EETypePtr interfaceType, ushort slot)
        {
            // Type of object we're dispatching on.
            EEType* pInstanceType = instanceType.ToPointer();

            // Type of interface
            EEType* pInterfaceType = interfaceType.ToPointer();

            return DispatchResolve.FindInterfaceMethodImplementationTarget(pInstanceType,
                                                                          pInterfaceType,
                                                                          slot);
        }

        private static unsafe IntPtr RhResolveDispatchWorker(object pObject, void* cell, ref DispatchCellInfo cellInfo)
        {
            // Type of object we're dispatching on.
            EEType* pInstanceType = pObject.EEType;

            if (cellInfo.CellType == DispatchCellType.InterfaceAndSlot)
            {
                // Type whose DispatchMap is used. Usually the same as the above but for types which implement ICastable
                // we may repeat this process with an alternate type.
                EEType* pResolvingInstanceType = pInstanceType;

                IntPtr pTargetCode = DispatchResolve.FindInterfaceMethodImplementationTarget(pResolvingInstanceType,
                                                                              cellInfo.InterfaceType.ToPointer(),
                                                                              cellInfo.InterfaceSlot);

                if (pTargetCode == IntPtr.Zero && pInstanceType->IsICastable)
                {
                    // TODO!! BEGIN REMOVE THIS CODE WHEN WE REMOVE ICASTABLE
                    // Dispatch not resolved through normal dispatch map, try using the ICastable
                    // Call the ICastable.IsInstanceOfInterface method directly rather than via an interface
                    // dispatch since we know the method address statically. We ignore any cast error exception
                    // object passed back on failure (result == false) since IsInstanceOfInterface never throws.
                    IntPtr pfnIsInstanceOfInterface = pInstanceType->ICastableIsInstanceOfInterfaceMethod;
                    Exception castError = null;
                    if (CalliIntrinsics.Call<bool>(pfnIsInstanceOfInterface, pObject, cellInfo.InterfaceType.ToPointer(), out castError))
                    {
                        IntPtr pfnGetImplTypeMethod = pInstanceType->ICastableGetImplTypeMethod;
                        pResolvingInstanceType = (EEType*)CalliIntrinsics.Call<IntPtr>(pfnGetImplTypeMethod, pObject, new IntPtr(cellInfo.InterfaceType.ToPointer()));
                        pTargetCode = DispatchResolve.FindInterfaceMethodImplementationTarget(pResolvingInstanceType,
                                                                                 cellInfo.InterfaceType.ToPointer(),
                                                                                 cellInfo.InterfaceSlot);
                    }
                    else
                    // TODO!! END REMOVE THIS CODE WHEN WE REMOVE ICASTABLE
                    {
                        // Dispatch not resolved through normal dispatch map, using the CastableObject path
                        pTargetCode = InternalCalls.RhpGetCastableObjectDispatchHelper();
                    }
                }

                return pTargetCode;
            }
            else if (cellInfo.CellType == DispatchCellType.VTableOffset)
            {
                // Dereference VTable
                return *(IntPtr*)(((byte*)pInstanceType) + cellInfo.VTableOffset);
            }
            else
            {
#if SUPPORTS_NATIVE_METADATA_TYPE_LOADING_AND_SUPPORTS_TOKEN_BASED_DISPATCH_CELLS
                // Attempt to convert dispatch cell to non-metadata form if we haven't acquired a cache for this cell yet
                if (cellInfo.HasCache == 0)
                {
                    cellInfo = InternalTypeLoaderCalls.ConvertMetadataTokenDispatch(InternalCalls.RhGetModuleFromPointer(cell), cellInfo);
                    if (cellInfo.CellType != DispatchCellType.MetadataToken)
                    {
                        return RhResolveDispatchWorker(pObject, cell, ref cellInfo);
                    }
                }

                // If that failed, go down the metadata resolution path
                return InternalTypeLoaderCalls.ResolveMetadataTokenDispatch(InternalCalls.RhGetModuleFromPointer(cell), (int)cellInfo.MetadataToken, new IntPtr(pInstanceType));
#else
                EH.FallbackFailFast(RhFailFastReason.InternalError, null);
                return IntPtr.Zero;
#endif
            }
        }
    }
}
