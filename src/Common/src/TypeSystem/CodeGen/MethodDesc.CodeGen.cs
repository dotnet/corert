// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;

namespace Internal.TypeSystem
{
    // Additional members of MethodDesc related to code generation.
    public abstract partial class MethodDesc
    {
        /// <summary>
        /// Gets a value specifying whether this method is an intrinsic.
        /// This can either be an intrinsic recognized by the compiler,
        /// by the codegen backend, or some other component.
        /// </summary>
        public virtual bool IsIntrinsic
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value specifying whether this method should not be included
        /// into the code of any caller methods by the compiler (and should be kept
        /// as a separate routine).
        /// </summary>
        public virtual bool IsNoInlining
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value specifying whether this method should be included into
        /// the code of the caller methods aggressively.
        /// </summary>
        public virtual bool IsAggressiveInlining
        {
            get
            {
                return false;
            }
        }
    }
}
