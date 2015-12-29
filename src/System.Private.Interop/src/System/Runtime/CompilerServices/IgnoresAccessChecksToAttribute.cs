// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text;

namespace System.Runtime.CompilerServices
{
    [AttributeUsageAttribute(AttributeTargets.Assembly, AllowMultiple = true)]
    public class IgnoresAccessChecksToAttribute : Attribute
    {
        private string _assemblyName;
        public IgnoresAccessChecksToAttribute(string assemblyName)
        {
            _assemblyName = assemblyName;
        }
        public string AssemblyName 
        { 
            get { return _assemblyName; } 
        } 
    }
}
