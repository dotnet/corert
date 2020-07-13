// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Reflection.Runtime.TypeInfos;
using System.Reflection.Runtime.TypeInfos.EcmaFormat;
using System.Reflection.Runtime.Assemblies;

using Internal.LowLevelLinq;
using Internal.Runtime.Augments;
using Internal.Reflection.Core.Execution;
using Internal.Reflection.Core.NonPortable;

namespace System.Reflection.Runtime.General
{
    internal static partial class Helpers
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EcmaFormatRuntimeNamedTypeInfo CastToEcmaFormatRuntimeNamedTypeInfo(this Type type)
        {
            Debug.Assert(type is EcmaFormatRuntimeNamedTypeInfo);
            return (EcmaFormatRuntimeNamedTypeInfo)type;
        }
    }
}
