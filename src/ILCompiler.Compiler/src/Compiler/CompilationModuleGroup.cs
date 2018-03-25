﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;

using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler
{
    public enum ExportForm
    {
        None = 0,
        ByName = 1,
        ByOrdinal = 2,
    }

    public abstract class CompilationModuleGroup
    {
        /// <summary>
        /// If true, "type" is in the set of input assemblies being compiled
        /// </summary>
        public abstract bool ContainsType(TypeDesc type);
        /// <summary>
        /// If true, type dictionary of "type" is in the module to be compiled
        /// </summary>
        public abstract bool ContainsTypeDictionary(TypeDesc type);
        /// <summary>
        /// If true, "method" is in the set of input assemblies being compiled
        /// </summary>
        public abstract bool ContainsMethodBody(MethodDesc method, bool unboxingStub);
        /// <summary>
        /// If true, the generic dictionary of "method" is in the set of input assemblies being compiled
        /// </summary>
        public abstract bool ContainsMethodDictionary(MethodDesc method);
        /// <summary>
        /// If true, "type" is exported by the set of input assemblies being compiled
        /// </summary>
        public abstract ExportForm GetExportTypeForm(TypeDesc type);
        /// <summary>
        /// If true, generic dictionary of "type" is exported by the set of input assemblies being compiled
        /// </summary>
        public abstract ExportForm GetExportTypeFormDictionary(TypeDesc type);
        /// <summary>
        /// If true, "method" is exported by the set of input assemblies being compiled
        /// </summary>
        public abstract ExportForm GetExportMethodForm(MethodDesc method, bool unboxingStub);
        /// <summary>
        /// If true, the generic dictionary of "method" is exported by the set of input assemblies being compiled
        /// </summary>
        public abstract ExportForm GetExportMethodDictionaryForm(MethodDesc method);
        /// <summary>
        /// If true, all code is compiled into a single module
        /// </summary>
        public abstract bool IsSingleFileCompilation { get; }
        /// <summary>
        /// If true, the full type should be generated. This occurs in situations where the type is 
        /// shared between modules (generics, parameterized types), or the type lives in a different module
        /// and therefore needs a full VTable
        /// </summary>
        public abstract bool ShouldProduceFullVTable(TypeDesc type);
        /// <summary>
        /// If true, the necessary type should be promoted to a full type should be generated. 
        /// </summary>
        public abstract bool ShouldPromoteToFullType(TypeDesc type);
        /// <summary>
        /// If true, if a type is in the dependency graph, its non-generic methods that can be transformed
        /// into code must be.
        /// </summary>
        public abstract bool PresenceOfEETypeImpliesAllMethodsOnType(TypeDesc type);
        /// <summary>
        /// If true, the type will not be linked into the same module as the current compilation and therefore
        /// accessed through the target platform's import mechanism (ie, Import Address Table on Windows)
        /// </summary>
        public abstract bool ShouldReferenceThroughImportTable(TypeDesc type);

        /// <summary>
        /// If true, there may be type system constructs that will not be linked into the same module as the current compilation and therefore
        /// accessed through the target platform's import mechanism (ie, Import Address Table on Windows)
        /// </summary>
        public abstract bool CanHaveReferenceThroughImportTable { get; }
    }
}
