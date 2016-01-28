// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//

using System;

namespace System.Runtime.InteropServices.WindowsRuntime
{
    // WindowsRuntimeImport is a pseudo custom attribute which causes us to emit the tdWindowsRuntime bit
    // onto types which are decorated with the attribute.  This is needed to mark Windows Runtime types
    // which are redefined in mscorlib.dll and System.Runtime.WindowsRuntime.dll, as the C# compiler does
    // not have a built in syntax to mark tdWindowsRuntime.   These two assemblies are special as they
    // implement the CLR's support for WinRT, so this type is internal as marking tdWindowsRuntime should
    // generally be done via winmdexp for user code.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Enum | AttributeTargets.Struct | AttributeTargets.Delegate, Inherited = false)]
    internal sealed class WindowsRuntimeImportAttribute : Attribute
    {
        public WindowsRuntimeImportAttribute()
        { }
    }
}
