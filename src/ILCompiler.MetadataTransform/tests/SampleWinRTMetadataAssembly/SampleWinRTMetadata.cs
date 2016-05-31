// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

#pragma warning disable 414
#pragma warning disable 67
#pragma warning disable 3009
#pragma warning disable 3016
#pragma warning disable 3001
#pragma warning disable 3015
#pragma warning disable 169
#pragma warning disable 649

namespace SampleMetadataWinRT
{
    // This class should appear to be in a regular managed module
    public class DerivedFromControl : Windows.Control
    { }

    // This class should appear to be in a winmd called SampleMetadataWinRT
    [global::Internal.Reflection.ExplicitScope("SampleMetadataWinRT, Version=255.255.255.255, Culture=neutral, PublicKeyToken=null, ContentType=WindowsRuntime")]
    public class DerivedFromControlAndInCustomScope : Windows.Control
    {
    }
}

