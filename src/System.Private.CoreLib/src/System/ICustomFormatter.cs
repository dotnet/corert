// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*============================================================
**
** Interface:  ICustomFormatter
**
**
** Purpose: Marks a class as providing special formatting
**
**
===========================================================*/

using System;

namespace System
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public interface ICustomFormatter
    {
        // Interface does not need to be marked with the serializable attribute
        String Format(String format, Object arg, IFormatProvider formatProvider);
    }
}
