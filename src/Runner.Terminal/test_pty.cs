using System;
using Pty.Net;

sealed class Program
{
    static void Main()
    {
        var type = typeof(Pty.Net.PtyProvider);
        foreach (var method in type.GetMethods())
        {
            Console.WriteLine(method.Name);
        }
    }
}
