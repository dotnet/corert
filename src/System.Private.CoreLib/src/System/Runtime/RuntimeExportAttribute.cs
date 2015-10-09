// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.Runtime
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    internal sealed class RuntimeExportAttribute : Attribute
    {
        public string EntryPoint;

        public RuntimeExportAttribute(string entry)
        {
            EntryPoint = entry;
        }
    }
}