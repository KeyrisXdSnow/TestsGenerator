
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Reflection;
using System.Threading;

namespace TestsGeneratorLib
{
    public class FileReader
    {
        private readonly int _threadAmount;
        private static List<Type> _types = new List<Type>();
        private static readonly object _stackLocker = new object();
        private static object _locker2 = new object();

        public FileReader(int threadAmount)
        {
            _threadAmount = threadAmount;
        }

        public void LoadClasses(Stack<string> paths)
        {
            for (var i = 0 ; i < _threadAmount; i++ )
            {
                var thread = new Thread(LoadClass);
                thread.Start(paths);
            }
            
        }

        private static void LoadClass(object paths)
        {
            var types = new List<Type>();
            var stack = (Stack<string>) paths;
            
            while (true)
            {
                bool isEmpty;
                lock (_stackLocker)
                {
                    isEmpty = ((Stack<string>) paths).Count == 0;
                }

                if (isEmpty) break;  
                
                string path;
                lock (_stackLocker)
                {
                    path = ((Stack<string>) paths).Pop();
                }

                //var asm = Assembly.LoadFrom(path);
                //types.AddRange(asm.GetTypes());
                Console.WriteLine(Thread.CurrentThread.ManagedThreadId + " " +path);
            }
            /// добавить в общую коллекцию
        }
    }
}