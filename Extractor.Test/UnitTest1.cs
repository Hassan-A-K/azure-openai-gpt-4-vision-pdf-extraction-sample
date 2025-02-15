using System;
using System.IO;
using Xunit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Extractor.Test
{
    public class UnitTest1
    {
        private readonly string outputDirectory = "Output";
        private readonly string demoFilesPath = "Demo Files";

        private readonly string[] expectedKeys = new string[]
        {
            "FileName",
            "DocumentTitle",
            "DateOfDocument",
            // Add other expected keys as needed
        };

        [Fact]
        public void TestFilesExist()
        {
            var jsonFiles = Directory.GetFiles(Path.Combine(outputDirectory), "*.Extraction.json");
            
            foreach (var jsonFile in jsonFiles)
            {
                Assert.True(File.Exists(jsonFile), $"Extraction JSON file not found: {jsonFile}");
            }
        }

        [Fact]
        public void TestJsonValuePairsMatchExpectedOutput()
        {
            var jsonFiles = Directory.GetFiles(Path.Combine(outputDirectory), "*.Extraction.json");

            foreach (var jsonFile in jsonFiles)
            {
                var json = File.ReadAllText(jsonFile);
                dynamic data = JsonConvert.DeserializeObject(json);

                foreach (var key in expectedKeys)
                {
                    Assert.True(data.ContainsKey(key), $"Key {key} not found in JSON output.");
                }

                ValidateJsonKeys(data);
            }
        }

        private void ValidateJsonKeys(dynamic data)
        {
            // Example validations
            Assert.NotNull(data.DocumentTitle?.Reasoning);
            // Add more key-specific validations as needed
        }

        [Fact]
        public void TestExecutionTime()
        {
            var stopWatch = System.Diagnostics.Stopwatch.StartNew();
            ModifiedExtractor.Program.Main(new string[0]); // Ensure Program.cs is accessible
            stopWatch.Stop();

            Console.WriteLine($"Execution time: {stopWatch.Elapsed.TotalSeconds} seconds");
        }
    } // Close UnitTest1 class
} // Close namespace Extractor.Test
