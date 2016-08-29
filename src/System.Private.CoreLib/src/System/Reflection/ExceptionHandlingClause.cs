// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace System.Reflection
{
    public class ExceptionHandlingClause
    {
        protected ExceptionHandlingClause() { }

        // Desktop compat: These default implementations behave strangely because this class was originally
        // creatable only from the native runtime, not through subclass inheritance.

        public virtual Type CatchType => null;
        public virtual int FilterOffset { get { throw new InvalidOperationException(); } }
        public virtual ExceptionHandlingClauseOptions Flags => default(ExceptionHandlingClauseOptions);
        public virtual int HandlerLength => 0;
        public virtual int HandlerOffset => 0;
        public virtual int TryLength => 0;
        public virtual int TryOffset => 0;

        public override string ToString()
        {
            return string.Format(CultureInfo.CurrentUICulture,
                "Flags={0}, TryOffset={1}, TryLength={2}, HandlerOffset={3}, HandlerLength={4}, CatchType={5}",
                Flags, TryOffset, TryLength, HandlerOffset, HandlerLength, CatchType);
        }
    }
}
