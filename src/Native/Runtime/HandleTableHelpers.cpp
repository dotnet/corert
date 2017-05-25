// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// Helper functions that are p/invoked from redhawkm in order to expose handle table functionality to managed
// code. These p/invokes are special in that the handle table code requires we remain in co-operative mode
// (since these routines mutate the handle tables which are also accessed during garbage collections). The
// binder has special knowledge of these methods and doesn't generate the normal code to transition out of the
// runtime prior to the call.
// 
#include "common.h"
#include "gcenv.h"
#include "objecthandle.h"
#include "RestrictedCallouts.h"


COOP_PINVOKE_HELPER(OBJECTHANDLE, RhpHandleAlloc, (Object *pObject, int type))
{
    return CreateTypedHandle(g_HandleTableMap.pBuckets[0]->pTable[GetCurrentThreadHomeHeapNumber()], pObject, type);
}

COOP_PINVOKE_HELPER(OBJECTHANDLE, RhpHandleAllocDependent, (Object *pPrimary, Object *pSecondary))
{
    return CreateDependentHandle(g_HandleTableMap.pBuckets[0]->pTable[GetCurrentThreadHomeHeapNumber()], pPrimary, pSecondary);
}

COOP_PINVOKE_HELPER(void, RhHandleFree, (OBJECTHANDLE handle))
{
    DestroyTypedHandle(handle);
}

COOP_PINVOKE_HELPER(Object *, RhHandleGet, (OBJECTHANDLE handle))
{
    return ObjectFromHandle(handle);
}

COOP_PINVOKE_HELPER(Object *, RhHandleGetDependent, (OBJECTHANDLE handle, Object **ppSecondary))
{
    Object *pPrimary = ObjectFromHandle(handle);
    *ppSecondary = (pPrimary != NULL) ? GetDependentHandleSecondary(handle) : NULL;
    return pPrimary;
}

COOP_PINVOKE_HELPER(void, RhHandleSetDependentSecondary, (OBJECTHANDLE handle, Object *pSecondary))
{
    SetDependentHandleSecondary(handle, pSecondary);
}

COOP_PINVOKE_HELPER(void, RhHandleSet, (OBJECTHANDLE handle, Object *pObject))
{
    StoreObjectInHandle(handle, pObject);
}

COOP_PINVOKE_HELPER(Boolean, RhRegisterRefCountedHandleCallback, (void * pCallout, EEType * pTypeFilter))
{
    return RestrictedCallouts::RegisterRefCountedHandleCallback(pCallout, pTypeFilter);
}

COOP_PINVOKE_HELPER(void, RhUnregisterRefCountedHandleCallback, (void * pCallout, EEType * pTypeFilter))
{
    RestrictedCallouts::UnregisterRefCountedHandleCallback(pCallout, pTypeFilter);
}

COOP_PINVOKE_HELPER(OBJECTHANDLE, RhpHandleAllocVariable, (Object * pObject, UInt32 type)) 
{
    return CreateVariableHandle(g_HandleTableMap.pBuckets[0]->pTable[GetCurrentThreadHomeHeapNumber()], pObject, type);
}

COOP_PINVOKE_HELPER(UInt32, RhHandleGetVariableType, (OBJECTHANDLE handle))
{
    return GetVariableHandleType(handle);
}

COOP_PINVOKE_HELPER(void, RhHandleSetVariableType, (OBJECTHANDLE handle, UInt32 type))
{
    UpdateVariableHandleType(handle, type);
}

COOP_PINVOKE_HELPER(UInt32, RhHandleCompareExchangeVariableType, (OBJECTHANDLE handle, UInt32 oldType, UInt32 newType))
{
    return CompareExchangeVariableHandleType(handle, oldType, newType);
}
