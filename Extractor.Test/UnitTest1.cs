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

        private readonly string expectedJsonPart1 = @"
        {
          ""FileName"": ""Part 1"",
          ""DocumentTitle"": {
            ""Reasoning"": ""The title \u0027OPERATING INSTRUCTIONS\u0027 is prominently displayed at the top of the first page."",
            ""Citation"": ""/Users/hak/Documents/GitHub/azure-openai-gpt-4-vision-pdf-extraction-sample/modifiedExtractor/Demo Files/Part 1.pdf"",
            ""Confidence"": 0.9,
            ""Value"": ""OPERATING INSTRUCTIONS""
          },
          ""DateOfDocument"": {
            ""Reasoning"": ""The date \u002715.05.1992\u0027 is found at the bottom of the table of contents page."",
            ""Citation"": ""/Users/hak/Documents/GitHub/azure-openai-gpt-4-vision-pdf-extraction-sample/modifiedExtractor/Demo Files/Part 1.pdf"",
            ""Confidence"": 0.9,
            ""Value"": ""15.05.1992""
          },
          ""DocumentRevision"": {
            ""Reasoning"": ""No specific revision number is mentioned in the document."",
            ""Citation"": null,
            ""Confidence"": 0,
            ""Value"": null
          },
          ""DocumentType"": {
            ""Reasoning"": ""The document is an \u0027OPERATING INSTRUCTIONS\u0027 manual."",
            ""Citation"": ""/Users/hak/Documents/GitHub/azure-openai-gpt-4-vision-pdf-extraction-sample/modifiedExtractor/Demo Files/Part 1.pdf"",
            ""Confidence"": 0.9,
            ""Value"": ""MAN - Manual""
          },
          ""DocumentType2"": {
            ""Reasoning"": null,
            ""Citation"": null,
            ""Confidence"": 0,
            ""Value"": null
          },
          ""DocumentType3"": {
            ""Reasoning"": null,
            ""Citation"": null,
            ""Confidence"": 0,
            ""Value"": null
          },
          ""Discipline"": {
            ""Reasoning"": ""The document pertains to mechanical equipment, specifically a machine."",
            ""Citation"": ""/Users/hak/Documents/GitHub/azure-openai-gpt-4-vision-pdf-extraction-sample/modifiedExtractor/Demo Files/Part 1.pdf"",
            ""Confidence"": 0.9,
            ""Value"": ""MEC - Mechanical""
          },
          ""Discipline2"": {
            ""Reasoning"": null,
            ""Citation"": null,
            ""Confidence"": 0,
            ""Value"": null
          },
          ""Discipline3"": {
            ""Reasoning"": null,
            ""Citation"": null,
            ""Confidence"": 0,
            ""Value"": null
          },
          ""LegacyNumber"": {
            ""Reasoning"": ""No legacy number is mentioned in the document."",
            ""Citation"": null,
            ""Confidence"": 0,
            ""Value"": null
          },
          ""Equipment"": {
            ""Reasoning"": ""The equipment type is \u0027Trigonal-Machine\u0027 with type \u0027SM-D3/HK\u0027."",
            ""Citation"": ""/Users/hak/Documents/GitHub/azure-openai-gpt-4-vision-pdf-extraction-sample/modifiedExtractor/Demo Files/Part 1.pdf"",
            ""Confidence"": 0.9,
            ""Value"": ""Trigonal-Machine SM-D3/HK""
          },
          ""SubEquipment"": {
            ""Reasoning"": ""No specific sub-equipment is mentioned in the document."",
            ""Citation"": null,
            ""Confidence"": 0,
            ""Value"": null
          },
          ""TagNumber"": {
            ""Reasoning"": ""No tag number matching the specified formats is found in the document."",
            ""Citation"": null,
            ""Confidence"": 0,
            ""Value"": null
          },
          ""ProjectID_AFE"": {
            ""Reasoning"": ""No project ID or AFE number is mentioned in the document."",
            ""Citation"": null,
            ""Confidence"": 0,
            ""Value"": null
          },
          ""FacilityCode"": {
            ""Reasoning"": ""No facility code is mentioned in the document."",
            ""Citation"": null,
            ""Confidence"": 0,
            ""Value"": null
          },
          ""ThirdPartyName"": {
            ""Reasoning"": ""The third party name \u0027SIEFER AMERICA\u0027 and \u0027HEATEC INC.\u0027 are mentioned as the customer."",
            ""Citation"": ""/Users/hak/Documents/GitHub/azure-openai-gpt-4-vision-pdf-extraction-sample/modifiedExtractor/Demo Files/Part 1.pdf"",
            ""Confidence"": 0.9,
            ""Value"": ""SIEFER AMERICA | HEATEC INC.""
          }
        }
        ";

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
        public void TestJsonKeysExist()
        {
            var jsonFiles = Directory.GetFiles(Path.Combine(outputDirectory), "*.Extraction.json");

            foreach (var jsonFile in jsonFiles)
            {
                var json = File.ReadAllText(jsonFile);
                dynamic data = JsonConvert.DeserializeObject(json);

                foreach (var key in expectedKeys)
                {
                    Assert.True(data.ContainsKey(key), $"Key '{key}' not found in JSON output: {jsonFile}");
                }
            }
        }


        [Fact]
        public void TestJsonValuePairsMatchExpectedOutput()
        {
            dynamic expectedDataPart1 = JsonConvert.DeserializeObject(expectedJsonPart1);
            var jsonFiles = Directory.GetFiles(Path.Combine(outputDirectory), "*.Extraction.json");

            foreach (var jsonFile in jsonFiles)
            {
                var json = File.ReadAllText(jsonFile);
                dynamic generatedData = JsonConvert.DeserializeObject(json);
                string fileName = Path.GetFileNameWithoutExtension(jsonFile).Replace(".Extraction", "");


                if (fileName == "Part 1") // Specific check for Part 1.pdf
                {
                    CompareJsonValues(expectedDataPart1, generatedData, jsonFile);
                }
                // You can add 'else if' blocks here for other files and their expected outputs if needed.
            }
        }


        private void CompareJsonValues(dynamic expectedData, dynamic generatedData, string jsonFile)
        {
            foreach (string key in expectedKeys)
            {
                Assert.True(generatedData.ContainsKey(key), $"Key '{key}' not found in generated JSON: {jsonFile}");

                if (expectedData[key] is JObject expectedObject)
                {
                    if (generatedData[key] is JObject generatedObject)
                    {
                       Assert.True(string.Equals(Convert.ToString(expectedObject["Value"]), Convert.ToString(generatedObject["Value"])), $"Value mismatch for key '{key}' in {jsonFile}");
                    }
                    else
                    {
                        Assert.Fail($"Expected nested object for key '{key}' but found different type in {jsonFile}");
                    }
                }
                else
                {
                     Assert.True(string.Equals(Convert.ToString(expectedData[key]), Convert.ToString(generatedData[key])), $"Value mismatch for key '{key}' in {jsonFile}");
                }
            }
        }


        [Fact]
        public void TestExecutionTime()
        {
            var stopWatch = System.Diagnostics.Stopwatch.StartNew();
            // Assuming your main program is in ModifiedExtractor.Program and accessible
            ModifiedExtractor.Program.Main(new string[0]);
            stopWatch.Stop();

            Console.WriteLine($"Execution time: {stopWatch.Elapsed.TotalSeconds} seconds");
        }
    } // Close UnitTest1 class
} // Close namespace Extractor.Test