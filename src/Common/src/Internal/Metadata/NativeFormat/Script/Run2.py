# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

"""
A convenience script used to drive each individual source generation script. Replace by
batch script GenerateNativeFormatSources.bat.
"""

import sys
import os

if __name__ == '__main__':
    #Dynamically append current script path to PYTHONPATH
    sys.path.append(os.path.dirname(sys.argv[0]))

import CsNativeFormatGen2

if __name__ == '__main__':
    reload(CsNativeFormatGen2)
    from CsPublicGen2 import CsEmitSource as PublicEmitSource
    PublicEmitSource()
    reload(CsNativeFormatGen2)
    from CsReaderGen2 import CsEmitSource as ReaderEmitSource
    ReaderEmitSource()
    reload(CsNativeFormatGen2)
    from CsWriterGen2 import WriterGen
    WriterGen().CsEmitSource()
    from CsMdBinaryRWCommonGen import MdBinaryRWGen
    MdBinaryRWGen().CsEmitSource()
    from CsWalkerGen3 import Walker
    Walker().CsEmitSource()
