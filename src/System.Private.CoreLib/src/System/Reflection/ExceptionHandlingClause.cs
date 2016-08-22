// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Reflection
{
    public class ExceptionHandlingClause
    {
        protected ExceptionHandlingClause() { }
        public virtual Type CatchType { get { throw new NotImplementedException(); } }
        public virtual int FilterOffset { get { throw new NotImplementedException(); } }
        public virtual ExceptionHandlingClauseOptions Flags { get { throw new NotImplementedException(); } }
        public virtual int HandlerLength { get { throw new NotImplementedException(); } }
        public virtual int HandlerOffset { get { throw new NotImplementedException(); } }
        public virtual int TryLength { get { throw new NotImplementedException(); } }
        public virtual int TryOffset { get { throw new NotImplementedException(); } }

        public override string ToString() { throw new NotImplementedException(); }
    }
}
