using System;

public class Foo
{
    public int nonDeclaredInt;
    public int declaredInt = 3;

    private bool nonDeclaredPrivateBool;
    private bool declaredPrivateBool = false;

    static double nonDeclaredStaticDoubleField;
    static double declaredStaticDoubleField = 3.00;

    public int doStuff()
    {
        nonDeclaredInt = 1;
        this.nonDeclaredInt = 2;
        this.nonDeclaredPrivateBool = true;

        nonDeclaredStaticDoubleField = 5.92;
        string surpression = nonDeclaredPrivateBool + " " + declaredPrivateBool + nonDeclaredStaticDoubleField + declaredStaticDoubleField;
        return 1;
    }
}
