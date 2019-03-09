// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Debug = System.Diagnostics.Debug;

namespace ILCompiler.PEWriter
{
    /// <summary>
    /// Given a metadata blob from an input MSIL image, fix-up the RVAs in the
    /// Method and FieldRVA tables by applying the delta of their containing section
    /// position in the new image.
    /// </summary>
    public class MetadataRvaFixupBuilder
    {
        /// <summary>
        /// Copy the input MSIL image metadata blob to a new BlobBuilder and update Rvas in
        /// the methodDef and fieldRva tables by applying an adjustment to their Rva which 
        /// is the delta between the containing PE section's start Rva in the input versus the output.
        /// </summary>
        /// <param name="peReader">Input MSIL image reader</param>
        /// <param name="sectionRvaDelta">The difference in bytes between the metadata PE section 
        /// (ie, ".text") start Rva in the input MSIL image versus the output image</param>
        public static unsafe BlobBuilder Relocate(PEReader peReader, int sectionRvaDelta)
        {
            BlobBuilder builder = new BlobBuilder();
            BlobReader reader = new BlobReader(peReader.GetMetadata().Pointer, peReader.GetMetadata().Length);
            MetadataReader metadataReader = peReader.GetMetadataReader();

            //
            // methodDef table
            //

            int methodDefTableOffset = metadataReader.GetTableMetadataOffset(TableIndex.MethodDef);
            builder.WriteBytes(reader.CurrentPointer, methodDefTableOffset);
            RelocateTableRvas(builder, TableIndex.MethodDef, metadataReader, ref reader, sectionRvaDelta);

            //
            // fieldRva table
            //

            int fieldRvaTableOffset = metadataReader.GetTableMetadataOffset(TableIndex.FieldRva);
            builder.WriteBytes(reader.CurrentPointer, fieldRvaTableOffset - reader.Offset);
            RelocateTableRvas(builder, TableIndex.FieldRva, metadataReader, ref reader, sectionRvaDelta);

            // Copy the rest of the metadata blob
            builder.WriteBytes(reader.CurrentPointer, metadataReader.MetadataLength - reader.Offset);

            Debug.Assert(builder.Count == metadataReader.MetadataLength);

            return builder;
        }

        private unsafe static void RelocateTableRvas(BlobBuilder builder, TableIndex tableIndex, MetadataReader metadataReader, ref BlobReader reader, int sectionRvaDelta)
        {
            int tableMetadataOffset = metadataReader.GetTableMetadataOffset(tableIndex);
            int rowCount = metadataReader.GetTableRowCount(tableIndex);
            int rowSize = metadataReader.GetTableRowSize(tableIndex);

            reader.Offset = tableMetadataOffset;

            for (int i = 0; i < rowCount; i++)
            {
                Debug.Assert(builder.Count == reader.Offset);

                // Both metadata tables containing Rvas have the Rva in column 0 of the metadata row
                int methodRva = reader.ReadInt32();

                if (methodRva == 0)
                {
                    // Don't fix up 0 Rvas (abstract methods in the methodDef table)
                    builder.WriteInt32(0);
                }
                else
                {
                    builder.WriteInt32(methodRva + sectionRvaDelta);
                }

                // Skip the rest of the row
                int remainingBytes = rowSize - sizeof(int);
                builder.WriteBytes(reader.CurrentPointer, remainingBytes);
                reader.Offset += remainingBytes;
            }
        }
    }
}
