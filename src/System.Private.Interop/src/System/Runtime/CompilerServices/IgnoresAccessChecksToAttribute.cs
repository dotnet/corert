// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
