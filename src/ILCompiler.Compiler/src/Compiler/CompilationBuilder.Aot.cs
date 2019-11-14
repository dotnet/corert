// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ILCompiler
{
    partial class CompilationBuilder
    {
        // These need to provide reasonable defaults so that the user can optionally skip
        // calling the Use/Configure methods and still get something reasonable back.
        protected MetadataManager _metadataManager;
        protected InteropStubManager _interopStubManager = new EmptyInteropStubManager();
        protected VTableSliceProvider _vtableSliceProvider = new LazyVTableSliceProvider();
        protected DictionaryLayoutProvider _dictionaryLayoutProvider = new LazyDictionaryLayoutProvider();
        protected DebugInformationProvider _debugInformationProvider = new DebugInformationProvider();
        protected DevirtualizationManager _devirtualizationManager = new DevirtualizationManager();
        protected bool _methodBodyFolding;
        protected bool _singleThreaded;

        partial void InitializePartial()
        {
            _metadataManager = new EmptyMetadataManager(_context);
        }

        public CompilationBuilder UseMetadataManager(MetadataManager metadataManager)
        {
            _metadataManager = metadataManager;
            return this;
        }

        public CompilationBuilder UseInteropStubManager(InteropStubManager interopStubManager)
        {
            _interopStubManager = interopStubManager;
            return this;
        }

        public CompilationBuilder UseVTableSliceProvider(VTableSliceProvider provider)
        {
            _vtableSliceProvider = provider;
            return this;
        }

        public CompilationBuilder UseGenericDictionaryLayoutProvider(DictionaryLayoutProvider provider)
        {
            _dictionaryLayoutProvider = provider;
            return this;
        }

        public CompilationBuilder UseDevirtualizationManager(DevirtualizationManager manager)
        {
            _devirtualizationManager = manager;
            return this;
        }

        public CompilationBuilder UseDebugInfoProvider(DebugInformationProvider provider)
        {
            _debugInformationProvider = provider;
            return this;
        }

        public CompilationBuilder UseMethodBodyFolding(bool enable)
        {
            _methodBodyFolding = enable;
            return this;
        }

        public CompilationBuilder UseSingleThread(bool enable)
        {
            _singleThreaded = enable;
            return this;
        }

        public ILScannerBuilder GetILScannerBuilder(CompilationModuleGroup compilationGroup = null)
        {
            return new ILScannerBuilder(_context, compilationGroup ?? _compilationGroup, _nameMangler, GetILProvider());
        }
    }
}
