// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Runtime.InteropServices
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class DllImportAttribute : Attribute
    {
        private string _dllName;
        public string EntryPoint;
        public CharSet CharSet;             // Not used in Redhawk - defined for convenience
        public bool SetLastError;           // Not used in Redhawk - defined for convenience 
        public bool ExactSpelling;          // Not used in Redhawk - defined for convenience
        public CallingConvention CallingConvention;
        public bool BestFitMapping;
        public bool PreserveSig;
        public bool ThrowOnUnmappableChar;

        public DllImportAttribute(string dllName)
        {
            _dllName = dllName;
        }

        public string Value
        {
            get
            {
                return _dllName;
            }
        }
    }
}
