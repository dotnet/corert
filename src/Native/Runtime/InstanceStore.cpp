//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
#include "rhcommon.h"
#include "CommonTypes.h"
#include "daccess.h"
#include "CommonMacros.h"
#include "PalRedhawkCommon.h"
#include "PalRedhawk.h"
#include "assert.h"
#include "static_check.h"
#include "type_traits.hpp"
#include "slist.h"
#include "holder.h"
#include "Crst.h"
#include "InstanceStore.h"
#include "RWLock.h"
#include "RuntimeInstance.h"

#include "slist.inl"

InstanceStore::InstanceStore()
{
}

InstanceStore::~InstanceStore()
{
}

// static 
InstanceStore * InstanceStore::Create()
{
    NewHolder<InstanceStore> pInstanceStore = new InstanceStore();

    pInstanceStore->m_Crst.Init(CrstInstanceStore);

    pInstanceStore.SuppressRelease();
    return pInstanceStore;
}

void InstanceStore::Destroy()
{
    delete this;
}

void InstanceStore::Insert(RuntimeInstance * pRuntimeInstance)
{
    CrstHolder ch(&m_Crst);

    m_InstanceList.PushHead(pRuntimeInstance);
}

RuntimeInstance * InstanceStore::GetRuntimeInstance(HANDLE hPalInstance)
{
    CrstHolder ch(&m_Crst);

    for (SList<RuntimeInstance>::Iterator it = m_InstanceList.Begin(); it != m_InstanceList.End(); ++it)
    {
        if (it->GetPalInstance() == hPalInstance)
        {
            return *it;
        }
    }
    return NULL;
}
