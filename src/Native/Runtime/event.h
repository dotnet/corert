//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
class CLREventStatic
{
public:
    void CreateManualEvent(bool bInitialState);
    void CreateAutoEvent(bool bInitialState);
    void CreateOSManualEvent(bool bInitialState);
    void CreateOSAutoEvent (bool bInitialState);
    void CloseEvent();
    bool IsValid() const;
    bool Set();
    bool Reset();
    uint32_t Wait(uint32_t dwMilliseconds, bool bAlertable, bool bAllowReentrantWait = false);
    HANDLE GetOSEvent();

private:
    HANDLE  m_hEvent;
    bool    m_fInitialized;
};
