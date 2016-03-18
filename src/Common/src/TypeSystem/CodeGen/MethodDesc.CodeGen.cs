// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

        /// <summary>
        /// Gets a value specifying whether the implementation of this method
        /// is provided by the runtime.
        /// </summary>
        public virtual bool IsRuntimeImplemented
        {
            get
            {
                return false;
            }
        }
    }

    // Additional members of InstantiatedMethod related to code generation.
    public partial class InstantiatedMethod
    {
        public override bool IsIntrinsic
        {
            get
            {
                return _methodDef.IsIntrinsic;
            }
        }

        public override bool IsNoInlining
        {
            get
            {
                return _methodDef.IsNoInlining;
            }
        }

        public override bool IsAggressiveInlining
        {
            get
            {
                return _methodDef.IsAggressiveInlining;
            }
        }

        public override bool IsRuntimeImplemented
        {
            get
            {
                return _methodDef.IsRuntimeImplemented;
            }
        }
    }

    // Additional members of MethodForInstantiatedType related to code generation.
    public partial class MethodForInstantiatedType
    {
        public override bool IsIntrinsic
        {
            get
            {
                return _typicalMethodDef.IsIntrinsic;
            }
        }

        public override bool IsNoInlining
        {
            get
            {
                return _typicalMethodDef.IsNoInlining;
            }
        }

        public override bool IsAggressiveInlining
        {
            get
            {
                return _typicalMethodDef.IsAggressiveInlining;
            }
        }

        public override bool IsRuntimeImplemented
        {
            get
            {
                return _typicalMethodDef.IsRuntimeImplemented;
            }
        }
    }

    // Additional members of ArrayMethod related to code generation.
    public partial class ArrayMethod
    {
        public override bool IsIntrinsic
        {
            get
            {
                return true;
            }
        }
    }
}
