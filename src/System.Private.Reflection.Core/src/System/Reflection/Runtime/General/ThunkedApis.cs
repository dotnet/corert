// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//
// The Reflection stack has grown a large legacy of apis that thunk others.
// Apis that do little more than wrap another api will be kept here to
// keep the main files less cluttered.
//

using System.IO;
using System.Text;
using System.Diagnostics;
using System.Globalization;
using System.Reflection.Runtime.General;

using Internal.LowLevelLinq;

namespace System.Reflection.Runtime.Assemblies
{
    internal partial class RuntimeAssembly
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

        public override string Location
        {
            get
            {
                if (AppContext.TryGetSwitch("Switch.System.Reflection.Assembly.SimulatedLocationInBaseDirectory", out bool isSimulated) && isSimulated)
                    return Path.Combine(AppContext.BaseDirectory, ManifestModule.Name);

                return string.Empty;
            }
        }

        public sealed override string CodeBase
        {
            get
            {
                var assemblyPath = Location;
                if (string.IsNullOrEmpty(assemblyPath))
                {
                    assemblyPath = Path.Combine(AppContext.BaseDirectory, typeof(object).Assembly.ManifestModule.Name);
                }
                assemblyPath = assemblyPath.Replace('\\', '/');
                if (assemblyPath.StartsWith('/'))
                {
                    return "file://" + assemblyPath;
                }
                return "file:///" + assemblyPath;
            }
        }

        public sealed override Assembly GetSatelliteAssembly(CultureInfo culture) { throw new PlatformNotSupportedException(); }
        public sealed override Assembly GetSatelliteAssembly(CultureInfo culture, Version version) { throw new PlatformNotSupportedException(); }
        public sealed override AssemblyName[] GetReferencedAssemblies() { throw new PlatformNotSupportedException(); }
        public sealed override Module GetModule(string name) { throw new PlatformNotSupportedException(); }
    }
}

namespace System.Reflection.Runtime.MethodInfos
{
    internal abstract partial class RuntimeConstructorInfo
    {
        public sealed override MethodImplAttributes GetMethodImplementationFlags() => MethodImplementationFlags;

        // Partial trust doesn't exist in Aot so these legacy apis are meaningless. Will report everything as SecurityCritical by fiat.
        public sealed override bool IsSecurityCritical => true;
        public sealed override bool IsSecuritySafeCritical => false;
        public sealed override bool IsSecurityTransparent => false;
    }
}

namespace System.Reflection.Runtime.EventInfos
{
    internal abstract partial class RuntimeEventInfo
    {
        public sealed override MethodInfo GetAddMethod(bool nonPublic) => AddMethod.FilterAccessor(nonPublic);
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

        // Partial trust doesn't exist in Aot so these legacy apis are meaningless. Will report everything as SecurityCritical by fiat.
        public sealed override bool IsSecurityCritical => true;
        public sealed override bool IsSecuritySafeCritical => false;
        public sealed override bool IsSecurityTransparent => false;
    }
}

namespace System.Reflection.Runtime.PropertyInfos
{
    internal abstract partial class RuntimePropertyInfo
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

        public sealed override string GetEnumName(object value) => Enum.GetName(this, value);
        public sealed override string[] GetEnumNames() => Enum.GetNames(this);
        public sealed override Type GetEnumUnderlyingType() => Enum.GetUnderlyingType(this);
        public sealed override Array GetEnumValues() => Enum.GetValues(this);
        public sealed override bool IsEnumDefined(object value) => Enum.IsDefined(this, value);

        // Partial trust doesn't exist in Aot so these legacy apis are meaningless. Will report everything as SecurityCritical by fiat.
        public sealed override bool IsSecurityCritical => true;
        public sealed override bool IsSecuritySafeCritical => false;
        public sealed override bool IsSecurityTransparent => false;

        public sealed override Type GetInterface(string name, bool ignoreCase)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            string simpleName;
            string ns;
            SplitTypeName(name, out simpleName, out ns);

            Type match = null;
            foreach (Type ifc in ImplementedInterfaces)
            {
                string ifcSimpleName = ifc.Name;
                bool simpleNameMatches = ignoreCase
                    ? (0 == CultureInfo.InvariantCulture.CompareInfo.Compare(simpleName, ifcSimpleName, CompareOptions.IgnoreCase))  // @todo: This could be expressed simpler but the necessary parts of String api not yet ported.
                    : simpleName.Equals(ifcSimpleName);
                if (!simpleNameMatches)
                    continue;

                // This check exists for desktop compat: 
                //   (1) caller can optionally omit namespace part of name in pattern- we'll still match. 
                //   (2) ignoreCase:true does not apply to the namespace portion.
                if (ns != null && !ns.Equals(ifc.Namespace))
                    continue;
                if (match != null)
                    throw new AmbiguousMatchException(SR.Arg_AmbiguousMatchException);
                match = ifc;
            }
            return match;
        }

        private static void SplitTypeName(string fullname, out string name, out string ns)
        {
            Debug.Assert(fullname != null);

            // Get namespace
            int nsDelimiter = fullname.LastIndexOf(".", StringComparison.Ordinal);
            if (nsDelimiter != -1)
            {
                ns = fullname.Substring(0, nsDelimiter);
                int nameLength = fullname.Length - ns.Length - 1;
                name = fullname.Substring(nsDelimiter + 1, nameLength);
                Debug.Assert(fullname.Equals(ns + "." + name));
            }
            else
            {
                ns = null;
                name = fullname;
            }
        }
    }
}

