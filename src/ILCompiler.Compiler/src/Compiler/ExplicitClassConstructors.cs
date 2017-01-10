// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Internal.TypeSystem;

using AssemblyName = System.Reflection.AssemblyName;
using Debug = System.Diagnostics.Debug;

namespace ILCompiler
{
    /// <summary>
    /// Encapsulates a list of class constructors that must be run in a prescribed order during start-up
    /// </summary>
    public sealed class ExplicitClassConstructors
    {
        private static readonly IList<OrderedClassConstructorInfo> OrderedClassConstructors =
            new ReadOnlyCollection<OrderedClassConstructorInfo>(new[]
            {
                new OrderedClassConstructorInfo("System.Private.CoreLib", "System.PreallocatedOutOfMemoryException"),
                new OrderedClassConstructorInfo("System.Private.CoreLib", "System.Runtime.CompilerServices.ClassConstructorRunner+Cctor"),
                new OrderedClassConstructorInfo("System.Private.CoreLib", "System.Runtime.CompilerServices.ClassConstructorRunner"),
                new OrderedClassConstructorInfo("System.Private.CoreLib", "System.Runtime.TypeCast+CastCache"),
                new OrderedClassConstructorInfo("System.Private.TypeLoader", "Internal.Runtime.TypeLoader.TypeLoaderEnvironment", false),
                new OrderedClassConstructorInfo("System.Private.CoreLib", "System.Runtime.TypeLoaderExports"),
                new OrderedClassConstructorInfo("System.Private.Reflection.Execution", "Internal.Reflection.Execution.ReflectionExecution"),
            });

        private List<TypeDesc> _typesWithExplicitCctors;
        private HashSet<TypeDesc> _hashTypesWithExplicitCctors;

        private readonly TypeSystemContext _context;
        private readonly bool _isCppCodeGen;

        public ExplicitClassConstructors(TypeSystemContext context, bool isCppCodeGen)
        {
            _context = context;
            //
            // We should not care which code-gen is being used, however currently CppCodeGen cannot
            // handle code pulled in by all explicit cctors.
            //
            _isCppCodeGen = isCppCodeGen;
        }

        public bool TypeHasExplicitClassConstructor(TypeDesc type)
        {
            if (_hashTypesWithExplicitCctors == null)
                InitExplicitCctors();

            return _hashTypesWithExplicitCctors.Contains(type);
        }

        public IList<TypeDesc> TypesWithExplicitCctors
        {
            get
            {
                if (_typesWithExplicitCctors == null)
                    InitExplicitCctors();

                return _typesWithExplicitCctors;
            }
        }

        private void InitExplicitCctors()
        {
            Debug.Assert(_typesWithExplicitCctors == null);
            Debug.Assert(_hashTypesWithExplicitCctors == null);

            _typesWithExplicitCctors = new List<TypeDesc>();
            _hashTypesWithExplicitCctors = new HashSet<TypeDesc>();
            foreach (var entry in OrderedClassConstructors)
            {
                if (_isCppCodeGen || !entry.UseWithCppCodeGen)
                    continue;

                ModuleDesc assembly = _context.ResolveAssembly(new AssemblyName(entry.Assembly));
                TypeDesc containingType = assembly.GetTypeByCustomAttributeTypeName(entry.TypeName);
                if (containingType == null)
                    throw new TypeLoadException(
                        string.Format($"Could not find type \"{entry.TypeName}\" to run ordered class constructor."));

                MethodDesc cctor = containingType.GetMethod(".cctor", null);
                if (cctor == null)
                    throw new MissingMethodException($"Could not find a class constructor on \"{entry.TypeName}\".");

                _typesWithExplicitCctors.Add(containingType);
                _hashTypesWithExplicitCctors.Add(containingType);
            }
        }

        private sealed class OrderedClassConstructorInfo
        {
            public string Assembly { get; }
            public string TypeName { get; }
            public bool UseWithCppCodeGen { get; }

            public OrderedClassConstructorInfo(string assembly, string typeName, bool useWithCppCodeGen = true)
            {
                Assembly = assembly;
                TypeName = typeName;
                UseWithCppCodeGen = useWithCppCodeGen;
            }
        }
    }
}
