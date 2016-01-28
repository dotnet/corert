// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

namespace Internal.Reflection.Core.NonPortable
{
    //
    // This class represents a pointer.
    //
    internal abstract class RuntimePointerType : RuntimeHasElementType
    {
        protected RuntimePointerType()
            : base()
        {
        }

        protected RuntimePointerType(RuntimeType runtimeTargetType)
            : base(runtimeTargetType)
        {
        }

        public sealed override bool IsPointer
        {
            get
            {
                return true;
            }
        }


        protected sealed override String Suffix
        {
            get
            {
                return "*";
            }
        }
    }
}

