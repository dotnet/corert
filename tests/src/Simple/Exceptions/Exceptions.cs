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
        int counter = 0;

        try
        {
            try
            {
                throw new Exception("My exception");
            }
            catch (OutOfMemoryException)
            {
                Console.WriteLine("Unexpected exception caught");
                return Fail;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Exception caught!");
            if (e.Message != "My exception")
            {
                 Console.WriteLine("Unexpected exception message!");
                 return Fail;
            }
            counter++;
        }

        try
        {
             try
             {
                 g.myField++;
             }
             finally
             {
                 counter++;
             }
        }
        catch (NullReferenceException)
        {
            Console.WriteLine("Null reference exception caught!");
            counter++;
        }

        try
        {
            throw new Exception("Testing filter");
        }
        catch (Exception e) when (e.Message == "Testing filter" && counter++ > 0)
        {
            Console.WriteLine("Exception caught via filter!");
            if (e.Message != "Testing filter")
            {
                 Console.WriteLine("Unexpected exception message!");
                 return Fail;
            }
            counter++;
        }

        if (counter != 5)
        {
            Console.WriteLine("Unexpected counter value");
            return Fail;
        }

        return Pass;
    }
}
