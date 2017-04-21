using System;

class Program
{
    static void ThrowExcThroughMethodsWithFinalizers1(string caller)
    {
        string s = caller + "+ ThrowExcThroughMethodsWithFinalizers1";
        try
        {
            ThrowExcThroughMethodsWithFinalizers2(s);
        }
        finally
        {
            Console.WriteLine("Executing finally in {0}", s);
        }
    }

    static void ThrowExcThroughMethodsWithFinalizers2(string caller)
    {
        string s = caller + "+ ThrowExcThroughMethodsWithFinalizers2";
        try
        {
            throw new Exception("my message");
        }
        finally
        {
            Console.WriteLine("Executing finally in {0}", s);
        }
    }

    static bool FilterWithGC()
    {
        GC.Collect();
        return true;
    }

    static void Main()
    {
        try
        {
            ThrowExcThroughMethodsWithFinalizers1("Main");
        }
        catch (Exception e) when (FilterWithGC())
        {
            Console.WriteLine(e.Message);
        }
    }
}