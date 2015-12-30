// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

//

//

namespace System.Runtime.InteropServices
{
    //====================================================================
    // The enum of the return value of IQuerable.GetInterface
    //====================================================================
    public enum CustomQueryInterfaceResult
    {
        Handled = 0,
        NotHandled = 1,
        Failed = 2,
    }
}
