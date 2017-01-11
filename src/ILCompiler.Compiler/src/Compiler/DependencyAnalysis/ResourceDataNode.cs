using Internal.Text;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Data about individual manifest resources
    /// </summary>
    internal class ResourceIndexData
    {
        public ResourceIndexData(string assemblyName, string resourceName, int nativeOffset, int ecmaOffset, EcmaModule ecmaModule, int length)
        {
            AssemblyName = assemblyName;
            ResourceName = resourceName;
            NativeOffset = nativeOffset;
            EcmaOffset = ecmaOffset;
            EcmaModule = ecmaModule;
            Length = length;
        }

        /// <summary>
        /// Full name of the assembly that contains the resource
        /// </summary>
        public string AssemblyName { get; }

        /// <summary>
        /// Name of the resource
        /// </summary>
        public string ResourceName { get; }

        /// <summary>
        /// Offset of the resource within the native resource blob
        /// </summary>
        public int NativeOffset { get; }

        /// <summary>
        /// Offset of the resource within the .mresources section of the ECMA module
        /// </summary>
        public int EcmaOffset { get; }

        /// <summary>
        /// Module the resource is defined in
        /// </summary>
        public EcmaModule EcmaModule { get; }

        /// <summary>
        /// Length of the resource
        /// </summary>
        public int Length { get; }
    }

    /// <summary>
    /// Blob of data containing resources for all assemblies generated into the image.
    /// Resources are simply copied from the inputs and concatenated into this blob.
    /// All format information is provided by <see cref="ResourceIndexNode"/>
    /// </summary>
    internal class ResourceDataNode : ObjectNode, ISymbolNode
    {
        private HashSet<ModuleDesc> _modulesSeen;

        /// <summary>
        /// Resource index information generated while extracting resources into the data blob
        /// </summary>
        public List<ResourceIndexData> IndexData { get; } = new List<ResourceIndexData>();

        public ResourceDataNode(HashSet<ModuleDesc> modulesSeen)
        {
            _modulesSeen = modulesSeen;
            _endSymbol = new ObjectAndOffsetSymbolNode(this, 0, "__embedded_resourcedata_End", true);
        }

        private ObjectAndOffsetSymbolNode _endSymbol;
        public ISymbolNode EndSymbol => _endSymbol;

        public override bool IsShareable => false;

        public override ObjectNodeSection Section => ObjectNodeSection.ReadOnlyDataSection;

        public override bool StaticDependenciesAreComputed => true;

        public int Offset => 0;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix).Append("__embedded_resourcedata");
        }

        protected override string GetName() => this.GetMangledName();

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // This node has no relocations.
            if (relocsOnly)
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolNode[] { this });
            
            byte[] blob = GenerateResourceBlob(factory.CompilationModuleGroup, factory.TypeSystemContext);
            return new ObjectData(
                blob,
                Array.Empty<Relocation>(),
                1,
                new ISymbolNode[]
                {
                                this,
                                EndSymbol
                });
        }

        /// <summary>
        /// Extracts resources from all modules being compiled into a single blob and saves
        /// the information needed to create an index into that blob.
        /// </summary>
        private byte[] GenerateResourceBlob(CompilationModuleGroup compilationModuleGroup, CompilerTypeSystemContext typeSystemContext)
        {
            int totalLength = 0;

            // Build up index information
            foreach (EcmaModule module in _modulesSeen.OfType<EcmaModule>())
            {
                PEMemoryBlock resourceDirectory = module.PEReader.GetSectionData(module.PEReader.PEHeaders.CorHeader.ResourcesDirectory.RelativeVirtualAddress);

                foreach (var resourceHandle in module.MetadataReader.ManifestResources)
                {
                    ManifestResource resource = module.MetadataReader.GetManifestResource(resourceHandle);
                    string resourceName = module.MetadataReader.GetString(resource.Name);
                    string assemblyName = GetAssemblyName(module);
                    BlobReader reader = resourceDirectory.GetReader(checked((int)resource.Offset), resourceDirectory.Length - (int)resource.Offset);
                    int length = checked((int)reader.ReadUInt32());
                    ResourceIndexData indexData = new ResourceIndexData(assemblyName, resourceName, totalLength, (int)resource.Offset + sizeof(Int32), module, length);
                    IndexData.Add(indexData);
                    totalLength += length;
                }

            }

            // Read resources into the blob
            byte[] resourceBlob = new byte[totalLength];
            int currentPos = 0;
            foreach (ResourceIndexData indexData in IndexData)
            {
                EcmaModule module = indexData.EcmaModule;
                PEMemoryBlock resourceDirectory = module.PEReader.GetSectionData(module.PEReader.PEHeaders.CorHeader.ResourcesDirectory.RelativeVirtualAddress);
                Debug.Assert(currentPos == indexData.NativeOffset);
                BlobReader reader = resourceDirectory.GetReader(indexData.EcmaOffset, indexData.Length);
                byte[] resourceData = reader.ReadBytes(indexData.Length);
                Buffer.BlockCopy(resourceData, 0, resourceBlob, currentPos, resourceData.Length);
                currentPos += resourceData.Length;
            }

            _endSymbol.SetSymbolOffset(resourceBlob.Length);
            return resourceBlob;
        }

        /// <summary>
        /// Gets the full assembly name of a module
        /// </summary>
        private string GetAssemblyName(EcmaModule module)
        {
            MetadataReader reader = module.MetadataReader;
            AssemblyDefinition assemblyDefinition = reader.GetAssemblyDefinition();
            string simpleName = reader.GetString(assemblyDefinition.Name);
            string culture = reader.GetString(assemblyDefinition.Culture);
            Version version = assemblyDefinition.Version;
            byte[] publicKey = reader.GetBlobBytes(assemblyDefinition.PublicKey);

            AssemblyName assemblyName = new AssemblyName();
            assemblyName.Name = simpleName;
            assemblyName.CultureName = culture;
            assemblyName.Version = version;
            assemblyName.SetPublicKey(publicKey);

            return assemblyName.FullName;
        }
    }
}