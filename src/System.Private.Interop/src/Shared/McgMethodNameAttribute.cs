// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace System.Runtime.InteropServices
{
    /// <summary>
    /// In the case of a WinRT name conflict, methods are renamed to something like the following:
    ///     IObservableVector`1<Foo>.GetAt
    /// C# obviously doesn't like this syntax. In MCG, we had to choose a different name, encode the real
    /// name in this attribute, and later rename the method in a IL transform
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class McgMethodNameAttribute : Attribute
    {
        public McgMethodNameAttribute(string realName)
        {
        }
    }
}
