using System;
using System.Collections.Generic;
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
        private List<TestResult> testResults = new List<TestResult>();

        public UnitTest1()
        {
            // Initialize the expected keys by hardcoding them
            expectedKeys = new string[]
            {
                "FileName",
                "DocumentTitle",
                "DateOfDocument",
                "DocumentRevision",
                "DocumentType",
                "DocumentType2",
                "DocumentType3",
                "Discipline",
                "Discipline2",
                "Discipline3",
                "LegacyNumber",
                "Equipment",
                "SubEquipment",
                "TagNumber",
                "ProjectID_AFE",
                "FacilityCode",
                "ThirdPartyName"
            };

            if (expectedKeys == null)
                throw new ArgumentNullException(nameof(expectedKeys), "Expected keys array is null.");
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
                    string generatedValueStr = Convert.ToString(generatedData[key])!;
                    if (!string.Equals(expectedValueStr, generatedValueStr))
                        Assert.Fail($"Value mismatch for key '{key}' in {jsonFile}. Expected: {expectedValueStr}, Got: {generatedValueStr}");
                }
            }
        }

        [Fact]
        public void TestOnlyValuesMatchExpectedOutput()
        {
            testResults.Clear();

            var jsonFiles = Directory.GetFiles(Path.Combine(outputDirectory), "*.Extraction.json");

            foreach (var jsonFile in jsonFiles)
            {
                try
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
                catch (Exception ex)
                {
                    // Log the error and continue
                    testResults.Add(new TestResult(
                        "TestOnlyValuesMatchExpectedOutput",
                        Path.GetFileName(jsonFile),
                        "N/A",
                        "N/A",
                        ex.Message,
                        false
                    ));
                }
            }

            string resultsFilePath = Path.Combine(expectedOutputsPath, "TestResults.csv");
            SaveResultsToFile(resultsFilePath);

            // Print a summary
            int totalTests = testResults.Count;
            int passedTests = testResults.Count(r => r.PassFail);
            Console.WriteLine($"\nTest Summary:");
            Console.WriteLine($"{passedTests}/{totalTests} tests passed.");
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

                string expectedValueStr, generatedValueStr;

                // Get expected value
                if (expectedData[key] is JObject expectedObject)
                    expectedValueStr = Convert.ToString(expectedObject["Value"]!)!;
                else
                    expectedValueStr = Convert.ToString(expectedData[key]!)!;

                // Get generated value
                if (generatedData[key] is JObject generatedObject)
                    generatedValueStr = Convert.ToString(generatedObject["Value"]!)!;
                else
                    generatedValueStr = Convert.ToString(generatedData[key])!;

                // Compare and record results
                bool isMatch = string.Equals(expectedValueStr, generatedValueStr);

                // Add result to the list
                testResults.Add(new TestResult(
                    "TestOnlyValuesMatchExpectedOutput",
                    Path.GetFileName(jsonFile),
                    key,
                    expectedValueStr,
                    generatedValueStr,
                    isMatch
                ));

                // Fail if mismatch
                if (!isMatch)
                {
                    Assert.Fail($"Value mismatch for key '{key}' in {jsonFile}. Expected: {expectedValueStr}, Got: {generatedValueStr}");
                }
            }
        }

        private void SaveResultsToFile(string filePath)
        {
            // Ensure the directory exists
            string directoryPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            using (StreamWriter writer = new StreamWriter(filePath))
            {
                // Write header
                writer.WriteLine("Test Name,File,Key,Expected Value,Actual Value,Pass/Fail");

                // Write test results
                foreach (var result in testResults)
                {
                    writer.WriteLine(
                        $"{result.TestName},{result.FileName},{result.Key}," +
                        $"{EscapeCsv(result.ExpectedValue)},{EscapeCsv(result.ActualValue)}," +
                        $"{(result.PassFail ? "Pass" : "Fail")}"
                    );
                }
            }

            Console.WriteLine($"Test results saved to: {filePath}");
        }

        // Helper method to escape CSV values
        private string EscapeCsv(string value)
        {
            if (value.Contains(",") || value.Contains("\""))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
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

    public class TestResult
    {
        public string TestName { get; set; }
        public string FileName { get; set; }
        public string Key { get; set; }
        public string ExpectedValue { get; set; }
        public string ActualValue { get; set; }
        public bool PassFail { get; set; }

        public TestResult(string testName, string fileName, string key, string expectedValue, string actualValue, bool passFail)
        {
            TestName = testName;
            FileName = fileName;
            Key = key;
            ExpectedValue = expectedValue;
            ActualValue = actualValue;
            PassFail = passFail;
        }
    }
}