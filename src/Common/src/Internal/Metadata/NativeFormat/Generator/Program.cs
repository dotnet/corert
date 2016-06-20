// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

class Program
{
    static void Main(string[] args)
    {
        using (var writer = new PublicGen(@"NativeFormatReaderCommonGen.cs"))
        {
            writer.EmitSource();
        }

        using (var writer = new ReaderGen(@"NativeFormatReaderGen.cs"))
        {
            writer.EmitSource();
        }

        using (var writer = new WriterGen(@"..\..\..\..\..\ILCompiler.MetadataWriter\src\Internal\Metadata\NativeFormat\Writer\NativeFormatWriterGen.cs"))
        {
            writer.EmitSource();
        }

        using (var writer = new MdBinaryReaderGen(@"MdBinaryReaderGen.cs"))
        {
            writer.EmitSource();
        }

        using (var writer = new MdBinaryWriterGen(@"..\..\..\..\..\ILCompiler.MetadataWriter\src\Internal\Metadata\NativeFormat\Writer\MdBinaryWriterGen.cs"))
        {
            writer.EmitSource();
        }
    }
}
