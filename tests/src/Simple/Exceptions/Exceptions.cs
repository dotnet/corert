// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;

public class BringUpTest
{
    const int Pass = 100;
    const int Fail = -1;

    volatile int myField;

    public BringUpTest()
    {
        myField = 1;
    }

    static BringUpTest g = null;

    public static int Main()
    {
        try
        {
            try
            {
                throw new Exception();
            }
            catch (OutOfMemoryException)
            {
                return Fail;
            }
        }
        catch
        {
            Console.WriteLine("Exception caught!");
        }

        try
        {
             g.myField++;
        }
        catch (NullReferenceException)
        {
            Console.WriteLine("Null reference exception caught!");
        }

        return Pass;
    }
}
