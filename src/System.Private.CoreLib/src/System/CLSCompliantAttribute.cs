// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*=============================================================================
**
**
**
** Purpose: Container for assemblies.
**
**
=============================================================================*/

namespace System
{
    [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    public sealed class CLSCompliantAttribute : Attribute
    {
        private bool _compliant;

        public CLSCompliantAttribute(bool isCompliant)
        {
            _compliant = isCompliant;
        }
        public bool IsCompliant
        {
            get
            {
                return _compliant;
            }
        }
    }
}
