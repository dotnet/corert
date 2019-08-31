// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ILCompiler.Win32Resources
{
    public unsafe partial class ResourceData
    {
        /// <summary>
        /// This is an abstraction around a List of bytes which allows reading and writing the contents
        /// </summary>
        private class Mu
        {
            List<byte> _data;
            int _curIndex;
            bool _fixedSize;
            bool _readonly;

            public Mu()
            {
                _data = new List<byte>();
            }

            public Mu(int size)
            {
                _data = new List<byte>(new byte[size]);
                _fixedSize = true;
            }

            public Mu(byte[] data)
            {
                _data = new List<byte>(data);
                _readonly = true;
                _fixedSize = true;
            }

            public Mu(Mu otherMu)
            {
                _data = otherMu._data;
                _curIndex = otherMu._curIndex;
                _fixedSize = otherMu._fixedSize;
                _readonly = otherMu._readonly;
            }

            public int FileSize
            {
                get
                {
                    return _data.Count;
                }
            }
            public int FilePos
            {
                get { return _curIndex; }
                set { if (value < 0) throw new ArgumentOutOfRangeException(); _curIndex = value; }
            }

            public uint FilePosUnsigned
            {
                get { checked { return (uint)_curIndex; } }
                set { checked { FilePos = (int)value; } }
            }

            public static void MoveFilePos(Mu f, int n)
            {
                f.FilePos = n;
            }

            public static void MoveFilePos(Mu f, uint n)
            {
                f.FilePos = (int)n;
            }

            public static void MoveFilePos(Mu f, long n)
            {
                checked
                {
                    f.FilePos = (int)n;
                }
            }

            public static int MoveToEnd(Mu f)
            {
                f._curIndex = f._data.Count;
                return f.FilePos;
            }

            public static byte[] Read(Mu f, uint n)
            {
                checked
                {
                    return Read(f, (int)n);
                }
            }

            public static byte[] Read(Mu f, int n)
            {
                checked
                {
                    byte[] b = new byte[n];
                    f._data.CopyTo(f._curIndex, b, 0, n);
                    f._curIndex += n;
                    return b;
                }
            }

            public static void Write(Mu f, byte[] data)
            {
                Write(f, data, data.Length);
            }

            public static void Write(Mu f, byte[] data, int count)
            {
                checked
                {
                    if (f._readonly)
                        throw new NotSupportedException();

                    if (f._curIndex > f._data.Count)
                    {
                        if (f._fixedSize)
                            throw new NotSupportedException();

                        // File pointer has been set beyond current end of file, and then written to. Expand file on demand.
                        f._data.AddRange(new byte[f._curIndex - f._data.Count]);
                        Debug.Assert(f._data.Count == f._curIndex);
                    }

                    for (int i = 0; i < count; i++)
                    {
                        byte b = data[i];
                        if (f._curIndex < f._data.Count)
                        {
                            f._data[f._curIndex] = b;
                            f._curIndex++;
                        }
                        else
                        {
                            if (f._fixedSize)
                                throw new NotSupportedException();

                            f._data.Add(b);
                            f._curIndex++;
                            Debug.Assert(f._data.Count == f._curIndex);
                        }
                    }
                }
            }

            public static uint ReadUInt32(Mu f)
            {
                return BitConverter.ToUInt32(Read(f, sizeof(uint)), 0);
            }

            public static ushort ReadUInt16(Mu f)
            {
                return BitConverter.ToUInt16(Read(f, sizeof(ushort)), 0);
            }
        }
    }
}
