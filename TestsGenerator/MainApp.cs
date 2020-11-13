using System.Collections.Generic;
using TestsGeneratorLib;

namespace TestsGenerator
{
    internal class MainApp
    {
        public static void Main(string[] args)
        {
            Stack<string> stack = new Stack<string>() ;
            
            stack.Push("E:\\Sharaga\\SPP\\TestsGenerator\\TestsGenerator\\Classes\\ClassFormatter.cs");
            stack.Push("E:\\Sharaga\\SPP\\TestsGenerator\\TestsGenerator\\Classes\\ConstructorFormatter.cs");
            stack.Push("E:\\Sharaga\\SPP\\TestsGenerator\\TestsGenerator\\Classes\\FieldFormatter.cs");
            stack.Push("E:\\Sharaga\\SPP\\TestsGenerator\\TestsGenerator\\Classes\\MethodFormatter.cs");
            stack.Push("E:\\Sharaga\\SPP\\TestsGenerator\\TestsGenerator\\Classes\\PropertiesFormatter.cs");
            // for (int i = 0; i < 10; i++)
            // {
            //     stack.Push(i.ToString());
            // }
            


        }
    }
}