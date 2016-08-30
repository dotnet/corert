// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// The Reflection stack has grown a large legacy of apis that thunk others.
// Apis that do little more than wrap another api will be kept here to
// keep the main files less cluttered.
//

using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Globalization;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using System.Reflection.Runtime.General;

using Internal.LowLevelLinq;

namespace System.Reflection.Runtime.Assemblies
{
    internal sealed partial class RuntimeAssembly
    {
        public sealed override Type[] GetExportedTypes() => ExportedTypes.ToArray();
        public sealed override Module[] GetLoadedModules(bool getResourceModules) => Modules.ToArray();
        public sealed override Module[] GetModules(bool getResourceModules) => Modules.ToArray();
        public sealed override Type[] GetTypes() => DefinedTypes.ToArray();

        // "copiedName" only affects whether CodeBase is set to the assembly location before or after the shadow-copy. 
        // That concept is meaningless on .NET Native.
        public sealed override AssemblyName GetName(bool copiedName) => GetName();

        public sealed override Stream GetManifestResourceStream(Type type, string name)
        {
            StringBuilder sb = new StringBuilder();
            if (type == null)
            {
                if (name == null)
                    throw new ArgumentNullException(nameof(type));
            }
            else
            {
                string nameSpace = type.Namespace;
                if (nameSpace != null)
                {
                    sb.Append(nameSpace);
                    if (name != null)
                    {
                        sb.Append(Type.Delimiter);
                    }
                }
            }

            if (name != null)
            {
                sb.Append(name);
            }

            return GetManifestResourceStream(sb.ToString());
        }
    }
}

namespace System.Reflection.Runtime.MethodInfos
{
    internal abstract partial class RuntimeConstructorInfo
    {
        public sealed override MethodImplAttributes GetMethodImplementationFlags() => MethodImplementationFlags;
    }
}

namespace System.Reflection.Runtime.EventInfos
{
    internal sealed partial class RuntimeEventInfo
    {
        public sealed override MethodInfo GetAddMethod(bool nonPublic) =>  AddMethod.FilterAccessor(nonPublic);
        public sealed override MethodInfo GetRemoveMethod(bool nonPublic) => RemoveMethod.FilterAccessor(nonPublic);
        public sealed override MethodInfo GetRaiseMethod(bool nonPublic) => RaiseMethod?.FilterAccessor(nonPublic);
    }
}

namespace System.Reflection.Runtime.MethodInfos
{
    internal abstract partial class RuntimeMethodInfo
    {
        public sealed override MethodImplAttributes GetMethodImplementationFlags() => MethodImplementationFlags;
        public sealed override ICustomAttributeProvider ReturnTypeCustomAttributes => ReturnParameter;
    }
}

namespace System.Reflection.Runtime.PropertyInfos
{
    internal sealed partial class RuntimePropertyInfo
    {
        public sealed override MethodInfo GetGetMethod(bool nonPublic) => Getter?.FilterAccessor(nonPublic);
        public sealed override MethodInfo GetSetMethod(bool nonPublic) => Setter?.FilterAccessor(nonPublic);
        public sealed override MethodInfo[] GetAccessors(bool nonPublic)
        {
            MethodInfo getter = GetGetMethod(nonPublic);
            MethodInfo setter = GetSetMethod(nonPublic);
            int count = 0;
            if (getter != null)
                count++;
            if (setter != null)
                count++;
            MethodInfo[] accessors = new MethodInfo[count];
            int index = 0;
            if (getter != null)
                accessors[index++] = getter;
            if (setter != null)
                accessors[index++] = setter;
            return accessors;
        }
    }
}

namespace System.Reflection.Runtime.TypeInfos
{
    internal abstract partial class RuntimeTypeInfo
    {
        public sealed override Type[] GetGenericArguments()
        {
            if (IsConstructedGenericType)
                return GenericTypeArguments;
            if (IsGenericTypeDefinition)
                return GenericTypeParameters;
            return Array.Empty<Type>();
        }

        public sealed override bool IsGenericType => IsConstructedGenericType || IsGenericTypeDefinition;
        public sealed override Type[] GetInterfaces() => ImplementedInterfaces.ToArray();
    }
}

