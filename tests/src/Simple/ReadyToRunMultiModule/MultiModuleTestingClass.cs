// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace TestingNamespace {
    public class TestingClassInNamespace
    {
        int N;
        public TestingClassInNamespace(int n)
        {
            N = n;
        }

        public int GetN()
        {
            return N;
        }

        public class TestingClassNested
        {
            int N;
            public TestingClassNested(int n)
            {
                N = n;
            }

            public int GetN()
            {
                return N;
            }
        }
    }
}

public class TestingClass
{
    int N;
    public TestingClass(int n)
    {
        N = n;
    }

    public int GetN()
    {
        return N;
    }
}
