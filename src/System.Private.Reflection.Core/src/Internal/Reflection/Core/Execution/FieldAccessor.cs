// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using global::System;
using global::System.Collections.Generic;
using global::Internal.Metadata.NativeFormat;

using global::Internal.Reflection.Core.NonPortable;

namespace Internal.Reflection.Core.Execution
{
    //
    // This class abstracts the underlying Redhawk (or whatever execution engine) runtime that sets and gets fields.
    //
    public abstract class FieldAccessor
    {
        protected FieldAccessor() { }
        public abstract Object GetField(Object obj);
        public abstract void SetField(Object obj, Object value);
    }
}
