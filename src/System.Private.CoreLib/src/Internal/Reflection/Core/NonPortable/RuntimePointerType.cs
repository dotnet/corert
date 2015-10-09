// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

