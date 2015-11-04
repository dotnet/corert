//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

//
// Implementations of functions dealing with object layout related types.
//
#include "common.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "assert.h"
#include "RedhawkWarnings.h"
#include "PalRedhawkCommon.h"
#include "PalRedhawk.h"
#include "TargetPtrs.h"
#include "eetype.h"
#include "ObjectLayout.h"

#ifndef DACCESS_COMPILE
void Object::InitEEType(EEType * pEEType)
{
    ASSERT(NULL == m_pEEType);
    m_pEEType = pEEType;
}
#endif

UInt32 Array::GetArrayLength()
{
    return m_Length;
}

void* Array::GetArrayData()
{
    UInt8* pData = (UInt8*)this;
    pData += (get_EEType()->get_BaseSize() - sizeof(ObjHeader));
    return pData;
}

#ifndef DACCESS_COMPILE
void Array::InitArrayLength(UInt32 length)
{
    ASSERT(NULL == m_Length);
    m_Length = length;
}

void MDArray::InitMDArrayLength(UInt32 length)
{
    ASSERT(NULL == m_Length);
    m_Length = length;
}

void MDArray::InitMDArrayDimension(UInt32 dimension, UInt32 value)
{
    ASSERT(dimension < 32);
    ASSERT(m_Dimensions[dimension] == NULL);
    m_Dimensions[dimension] = value;
}

void ObjHeader::SetBit(UInt32 uBit)
{
    PalInterlockedOr(&m_uSyncBlockValue, uBit);
}

void ObjHeader::ClrBit(UInt32 uBit)
{
    PalInterlockedAnd(&m_uSyncBlockValue, ~uBit);
}

size_t Object::GetSize()
{
    EEType * pEEType = get_EEType();

    // strings have component size2, all other non-arrays should have 0
    ASSERT(( pEEType->get_ComponentSize() <= 2) || pEEType->IsArray());

    size_t s = pEEType->get_BaseSize();
    UInt16 componentSize = pEEType->get_ComponentSize();
    if (componentSize > 0)
        s += ((Array*)this)->GetArrayLength() * componentSize;
    return s;
}

#endif
