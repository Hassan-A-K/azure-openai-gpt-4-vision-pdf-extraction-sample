using System;
using System.IO;
using Xunit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Extractor.Test
{
    public class UnitTest1
    {
        private readonly string outputDirectory = Path.Combine("..", "..", "..", "..", "modifiedExtractor", "Output");
        private readonly string demoFilesPath = Path.Combine("..", "..", "..", "..", "modifiedExtractor", "Demo Files");
        private readonly string expectedOutputsPath = Path.Combine("..", "..", "..", "..", "Extractor.Test", "ExpectedOutputs");

        private string[]? expectedKeys;

        public UnitTest1()
        {
            // Initialize the expected keys by reading from the JSON file
            string expectedKeysFilePath = Path.Combine(expectedOutputsPath, "ExpectedKeys.json");

            // Check if the file exists
            Assert.True(File.Exists(expectedKeysFilePath), $"Expected keys file not found: {expectedKeysFilePath}");

            // Read the file content
            string expectedKeysJson = File.ReadAllText(expectedKeysFilePath);

            // Deserialize the JSON content
            expectedKeys = JsonConvert.DeserializeObject<string[]>(expectedKeysJson);

            if (expectedKeys == null)
                throw new JsonSerializationException("Failed to deserialize expected keys from JSON.");
        }

        [Fact]
        public void TestPdfFilesHaveExtractionJson()
        {
            var pdfFiles = Directory.GetFiles(demoFilesPath, "*.pdf");

            foreach (var pdfFile in pdfFiles)
            {
                string pdfFileName = Path.GetFileNameWithoutExtension(pdfFile);
                string expectedJsonFilePath = Path.Combine(outputDirectory, pdfFileName + ".Extraction.json");
                

                Assert.True(File.Exists(expectedJsonFilePath), $"Extraction JSON file not found for PDF: {pdfFile}");
            }
        }

        [Fact]
        public void TestJsonKeysExist()
        {
            var jsonFiles = Directory.GetFiles(Path.Combine(outputDirectory), "*.Extraction.json");

            foreach (var jsonFile in jsonFiles)
            {
                var json = File.ReadAllText(jsonFile);
                dynamic data = JsonConvert.DeserializeObject(json);

                if (data == null)
                    throw new JsonSerializationException($"Failed to deserialize JSON data for file: {jsonFile}");

                if (expectedKeys == null)
                    throw new ArgumentNullException(nameof(expectedKeys), "Expected keys array is null.");

                foreach (var key in expectedKeys)
                {
                    Assert.True(data.ContainsKey(key), $"Key '{key}' not found in JSON output: {jsonFile}");
                }
            }
        }

        [Fact]
        public void TestJsonValuePairsMatchExpectedOutput()
        {
            var jsonFiles = Directory.GetFiles(Path.Combine(outputDirectory), "*.Extraction.json");

            foreach (var jsonFile in jsonFiles)
            {
                var json = File.ReadAllText(jsonFile);
                dynamic generatedData = JsonConvert.DeserializeObject(json);

                if (generatedData == null)
                    throw new JsonSerializationException($"Failed to deserialize generated JSON data for file: {jsonFile}");

                string fileName = Path.GetFileNameWithoutExtension(jsonFile).Replace(".Extraction", "");

                // Construct the expected JSON file path
                string expectedJsonFilePath = Path.Combine(expectedOutputsPath, fileName + ".Extraction.json");

                // Ensure the expected JSON file exists
                Assert.True(File.Exists(expectedJsonFilePath), $"Expected JSON file not found for {fileName}: {expectedJsonFilePath}");

                // Read the expected JSON content
                string expectedJson = File.ReadAllText(expectedJsonFilePath);
                dynamic expectedData = JsonConvert.DeserializeObject(expectedJson);

                if (expectedData == null)
                    throw new JsonSerializationException($"Failed to deserialize expected JSON data for file: {expectedJsonFilePath}");

                CompareJsonValues(expectedData, generatedData, jsonFile);
            }
        }

        private void CompareJsonValues(dynamic expectedData, dynamic generatedData, string jsonFile)
        {
            if (generatedData == null) throw new ArgumentNullException(nameof(generatedData), $"Generated data is null for file: {jsonFile}");
            if (expectedData == null) throw new ArgumentNullException(nameof(expectedData), $"Expected data is null for file: {jsonFile}");

            if (expectedKeys == null)
                throw new ArgumentNullException(nameof(expectedKeys), "Expected keys array is null.");

            foreach (string key in expectedKeys)
            {
                Assert.True(generatedData.ContainsKey(key), $"Key '{key}' not found in generated JSON: {jsonFile}");

                if (expectedData[key] is JObject expectedObject)
                {
                    if (generatedData[key] is JObject generatedObject)
                    {
                        string expectedValueStr = Convert.ToString(expectedObject["Value"]!)!;
                        string generatedValueStr = Convert.ToString(generatedObject["Value"]!)!;
                        if (!string.Equals(expectedValueStr, generatedValueStr))
                            Assert.Fail($"Value mismatch for key '{key}' in {jsonFile}. Expected: {expectedValueStr}, Got: {generatedValueStr}");
                    }
                    else
                    {
                        Assert.Fail($"Expected nested object for key '{key}' but found different type in {jsonFile}");
                    }
                }
                else
                {
                    string expectedValueStr = Convert.ToString(expectedData[key]!)!;
                    string generatedValueStr = Convert.ToString(generatedData[key]!)!;
                    if (!string.Equals(expectedValueStr, generatedValueStr))
                        Assert.Fail($"Value mismatch for key '{key}' in {jsonFile}. Expected: {expectedValueStr}, Got: {generatedValueStr}");
                }
            }
        }

        [Fact]
        public void TestOnlyValuesMatchExpectedOutput()
        {
            var jsonFiles = Directory.GetFiles(Path.Combine(outputDirectory), "*.Extraction.json");

            foreach (var jsonFile in jsonFiles)
            {
                var json = File.ReadAllText(jsonFile);
                dynamic generatedData = JsonConvert.DeserializeObject(json);

                if (generatedData == null)
                    throw new JsonSerializationException($"Failed to deserialize generated JSON data for file: {jsonFile}");

                string fileName = Path.GetFileNameWithoutExtension(jsonFile).Replace(".Extraction", "");

                // Construct the expected JSON file path
                string expectedJsonFilePath = Path.Combine(expectedOutputsPath, fileName + ".Extraction.json");

                // Ensure the expected JSON file exists
                Assert.True(File.Exists(expectedJsonFilePath), $"Expected JSON file not found for {fileName}: {expectedJsonFilePath}");

                // Read the expected JSON content
                string expectedJson = File.ReadAllText(expectedJsonFilePath);
                dynamic expectedData = JsonConvert.DeserializeObject(expectedJson);

                if (expectedData == null)
                    throw new JsonSerializationException($"Failed to deserialize expected JSON data for file: {expectedJsonFilePath}");

                CompareOnlyValues(expectedData, generatedData, jsonFile);
            }
        }

        private void CompareOnlyValues(dynamic expectedData, dynamic generatedData, string jsonFile)
        {
            if (generatedData == null) throw new ArgumentNullException(nameof(generatedData), $"Generated data is null for file: {jsonFile}");
            if (expectedData == null) throw new ArgumentNullException(nameof(expectedData), $"Expected data is null for file: {jsonFile}");

            if (expectedKeys == null)
                throw new ArgumentNullException(nameof(expectedKeys), "Expected keys array is null.");

            foreach (string key in expectedKeys)
            {
                Assert.True(generatedData.ContainsKey(key), $"Key '{key}' not found in generated JSON: {jsonFile}");

                if (expectedData[key] is JObject expectedObject)
                {
                    string expectedValueStr = Convert.ToString(expectedObject["Value"]!)!;
                    string generatedValueStr = Convert.ToString(generatedData[key]["Value"]!)!;
                    if (!string.Equals(expectedValueStr, generatedValueStr))
                        Assert.Fail($"Value mismatch for key '{key}' in {jsonFile}. Expected: {expectedValueStr}, Got: {generatedValueStr}");
                }
                else
                {
                    string expectedValueStr = Convert.ToString(expectedData[key]!)!;
                    string generatedValueStr = Convert.ToString(generatedData[key])!;
                    if (!string.Equals(expectedValueStr, generatedValueStr))
                        Assert.Fail($"Value mismatch for key '{key}' in {jsonFile}. Expected: {expectedValueStr}, Got: {generatedValueStr}");
                }
            }
        }

        [Fact]
        public void TestExecutionTime()
        {
            var stopWatch = System.Diagnostics.Stopwatch.StartNew();
            ModifiedExtractor.Program.Main(new string[0]);
            stopWatch.Stop();

            Console.WriteLine($"Execution time: {stopWatch.Elapsed.TotalSeconds} seconds");
        }
    }
}
