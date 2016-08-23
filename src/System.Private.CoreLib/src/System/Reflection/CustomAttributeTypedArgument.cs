// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Reflection
{
    public struct CustomAttributeTypedArgument
    {
        public CustomAttributeTypedArgument(object value)
        {
            // value cannot be null.
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            Value = CanonicalizeValue(value);
            ArgumentType = value.GetType();
        }

        public CustomAttributeTypedArgument(Type argumentType, object value)
        {
            // value can be null.
            if (argumentType == null)
                throw new ArgumentNullException(nameof(argumentType));

            Value = (value == null) ? null : CanonicalizeValue(value);
            ArgumentType = argumentType;
        }

        public Type ArgumentType { get; }
        public object Value { get; }

        public override bool Equals(object obj) => obj == (object)this;
        public override int GetHashCode() => base.GetHashCode();
        public static bool operator ==(CustomAttributeTypedArgument left, CustomAttributeTypedArgument right) => left.Equals(right);
        public static bool operator !=(CustomAttributeTypedArgument left, CustomAttributeTypedArgument right) => !(left.Equals(right));

        public override string ToString()
        {
            // base.ToString() is a temporary implementation: this silly looking line officially tags this method as needing further work.
            if (string.Empty.Length > 0) throw new NotImplementedException();
            return base.ToString();
        }

        private static object CanonicalizeValue(object value)
        {
            if (value.GetType().IsEnum)
                return ((Enum)value).GetValue();
            return value;
        }
    }
}
