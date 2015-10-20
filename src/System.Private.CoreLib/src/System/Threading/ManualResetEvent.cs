// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*=============================================================================
**
**
**
** Purpose: An example of a WaitHandle class
**
**
=============================================================================*/

using System;
using System.Runtime.InteropServices;

namespace System.Threading
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public sealed class ManualResetEvent : EventWaitHandle
    {
        public ManualResetEvent(bool initialState) : base(initialState, EventResetMode.ManualReset) { }
    }
}

