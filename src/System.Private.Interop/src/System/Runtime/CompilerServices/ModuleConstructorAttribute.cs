// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace System.Runtime.CompilerServices
{
    // If a class is marked with [ModuleConstructorAttribute], we will treat this class's .cctor as module .cctor during ILTransform since C# doesn't support write module .cctor directly
    // We can use this attribute to control initialize order inside a module and module dependency(in StartUpCodeInjectorTransform) controls initialize order between modules
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ModuleConstructorAttribute : Attribute
    {
        public ModuleConstructorAttribute()
        {
        }
    }
}
