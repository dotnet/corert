// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

using Internal.LowLevelLinq;

namespace System.Reflection.Runtime.BindingFlagSupport
{
    internal sealed partial class DefaultBinder : Binder
    {
        public sealed override FieldInfo BindToField(BindingFlags bindingAttr, FieldInfo[] match, object value, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public sealed override MethodBase BindToMethod(BindingFlags bindingAttr, MethodBase[] match, ref object[] args, ParameterModifier[] modifiers, CultureInfo culture, string[] names, out object state)
        {
            if (!((bindingAttr & ~SupportedBindingFlags) == 0) && modifiers == null && culture == null && names == null)
                throw new NotImplementedException();
            state = null;
            return LimitedBinder.BindToMethod(match, ref args);
        }

        public sealed override object ChangeType(object value, Type type, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public sealed override void ReorderArgumentArray(ref object[] args, object state)
        {
            throw new NotImplementedException();
        }

        public sealed override MethodBase SelectMethod(BindingFlags bindingAttr, MethodBase[] match, Type[] types, ParameterModifier[] modifiers)
        {
            if (!((bindingAttr & ~SupportedBindingFlags) == 0) && modifiers == null)
                throw new NotImplementedException();

            return LimitedBinder.SelectMethod(match, types);
        }

        public sealed override PropertyInfo SelectProperty(BindingFlags bindingAttr, PropertyInfo[] match, Type returnType, Type[] indexes, ParameterModifier[] modifiers)
        {
            if (!((bindingAttr & ~SupportedBindingFlags) == 0) && modifiers == null)
                throw new NotImplementedException();

            PropertyInfo[] memberArray = match.ToArray();

            if (memberArray.Length == 0)
                return null;

            return LimitedBinder.SelectProperty(memberArray, returnType, indexes);
        }

        private const BindingFlags SupportedBindingFlags = BindingFlags.IgnoreCase | BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy | BindingFlags.CreateInstance;
    }
}
