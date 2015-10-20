// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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