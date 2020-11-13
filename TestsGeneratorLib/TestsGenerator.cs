using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace TestsGeneratorLib
{
    public class TestsGenerator
    {
        private readonly int _readFileThreadsAmount;
        private readonly int _processedTasksThreadsAmount;
        private readonly int _writeFileThreadsAmount;

        public TestsGenerator(int readFileThreadsAmount, int processedTasksThreadsAmount, int writeFileThreadsAmount)
        {
            _readFileThreadsAmount = readFileThreadsAmount;
            _processedTasksThreadsAmount = processedTasksThreadsAmount;
            _writeFileThreadsAmount = writeFileThreadsAmount;
        }

        public Task GenerateCLasses(IEnumerable<string> classPaths, string testPath)
        {
            var maxFilesToLoad = new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = _readFileThreadsAmount };
            var maxTestToGenerate = new ExecutionDataflowBlockOptions() {MaxDegreeOfParallelism = _processedTasksThreadsAmount};
            var maxFilesToWrite = new ExecutionDataflowBlockOptions() {MaxDegreeOfParallelism = _writeFileThreadsAmount};

            // Create a dataflow block that takes a  path as input and returns a file text
            var loadClasses = new TransformBlock<string, string> (GetTextFromFile,maxFilesToLoad); // add lambda and try/catch
            
            // Create a dataflow block that takes a file text and return generated Tests
            var generateTests = new TransformBlock<string, string[]>(GetTestFromText, maxTestToGenerate); // add lambda and try/catch
            
            // Create a dataflow block that save generated Test on disk 
            var writeTests = new ActionBlock<string[]>(WriteTests, maxFilesToLoad); // add lambda and try/catch
            
            //
            // Connect the dataflow blocks to form a pipeline.
            // 
            var linkOption = new DataflowLinkOptions
            {
                PropagateCompletion = true
            };
            loadClasses.LinkTo(generateTests, linkOption);
            generateTests.LinkTo(writeTests, linkOption);

            foreach (var path in classPaths)
            {
                loadClasses.Post(path);
            }
            
            // Mark the head of the pipeline as complete.
            loadClasses.Complete();

            return writeTests.Completion;
        }

        private static async Task<string> GetTextFromFile(string path)
        {
            using var sourceStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 4096, useAsync: true);

            var sb = new StringBuilder();

            var buffer = new byte[0x1000];
            int numRead;
            
            
            while ((numRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
            {
                var text = Encoding.UTF8.GetString(buffer, 0, numRead);
                sb.Append(text);
            }

            return sb.ToString();
        }

        private static async Task<string[]> GetTestFromText(string text)
        {
            return null;
        }

        private static async Task WriteTests(string[] tests)
        {
            var filePath = "";

            foreach (var test in tests)
            {
                var encodedText = Encoding.Unicode.GetBytes(test);

                using var sourceStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None,
                    bufferSize: 4096, useAsync: true);

                await sourceStream.WriteAsync(encodedText, 0, encodedText.Length);
            }
        }

    }
    
}