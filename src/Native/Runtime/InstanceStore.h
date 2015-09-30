//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
class RuntimeInstance;

class InstanceStore
{
    SList<RuntimeInstance>  m_InstanceList;
    CrstStatic              m_Crst;

private:
    InstanceStore();

public:
    ~InstanceStore();
    static InstanceStore *  Create();
    void                    Destroy();

    RuntimeInstance *       GetRuntimeInstance(HANDLE hPalInstance);

    void Insert(RuntimeInstance * pRuntimeInstance);
};

