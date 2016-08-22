// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace System.Reflection
{
    public class MethodBody
    {
        protected MethodBody() { }

        // Desktop compat: These default implementations behave strangely because this class was originally
        // creatable only from the native runtime, not through subclass inheritance.
        public virtual int LocalSignatureMetadataToken => 0;
        public virtual IList<LocalVariableInfo> LocalVariables { get { throw new ArgumentNullException("array"); } }
        public virtual int MaxStackSize => 0;
        public virtual bool InitLocals => false;
        public virtual byte[] GetILAsByteArray() => null;
        public virtual IList<ExceptionHandlingClause> ExceptionHandlingClauses { get { throw new ArgumentNullException("array"); } }
    }
}
