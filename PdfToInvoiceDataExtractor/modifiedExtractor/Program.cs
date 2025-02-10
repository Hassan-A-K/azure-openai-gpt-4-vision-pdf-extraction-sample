using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using DotNetEnv;
using SautinSoft; // Correct namespace
using SkiaSharp;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ModifiedExtractor
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Env.Load("config.env");

            var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
            var modelDeployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_COMPLETION_MODEL_DEPLOYMENT_NAME");
            var apiVersion = "2025-01-01-preview";

            // Construct the base URI without query parameters
            string baseUri = $"{endpoint}openai/deployments/{modelDeployment}/chat/completions";
            var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ExcludeEnvironmentCredential = true,
                ExcludeManagedIdentityCredential = true,
                ExcludeSharedTokenCacheCredential = true,
                ExcludeInteractiveBrowserCredential = true,
                ExcludeAzurePowerShellCredential = true,
                ExcludeVisualStudioCodeCredential = false,
                ExcludeAzureCliCredential = false
            });

            try
            {
                var bearerToken = credential.GetToken(new TokenRequestContext(new[] { "https://cognitiveservices.azure.com/.default" })).Token;

                string currentDirectory = Directory.GetCurrentDirectory();
                string demoFilesPath = Path.Combine(currentDirectory, "Demo Files");
                string diagramsFolderPath = Path.Combine(demoFilesPath, "");

                if (!Directory.Exists(diagramsFolderPath))
                {
                    Console.WriteLine($"Folder 'Demo Files/Diagrams' does not exist in the current directory.");
                    return;
                }

                string outputFolderPath = Path.Combine(currentDirectory, "Output");
                if (!Directory.Exists(outputFolderPath))
                {
                    Directory.CreateDirectory(outputFolderPath);
                }

                string systemPromptFilePath = Path.Combine(currentDirectory, "DCPromptsForAzureOpenAI Enchanced 3.txt");
                if (!File.Exists(systemPromptFilePath))
                {
                    Console.WriteLine($"File 'DCPromptsForAzureOpenAI.txt' does not exist in the current directory.");
                    return;
                }

                string systemPrompt = await File.ReadAllTextAsync(systemPromptFilePath);

                string[] pdfFiles = Directory.GetFiles(diagramsFolderPath, "*.pdf");

                foreach (string pdfFilePath in pdfFiles)
                {
                    string pdfName = Path.GetFileNameWithoutExtension(pdfFilePath);
                    string pdfJsonExtractionName = $"{pdfName}.Extraction.json";
                    string responseFileName = $"{pdfName}.Response.json";

                    PdfFocus f = new PdfFocus();
                    f.OpenPdf(pdfFilePath);

                    if (f.PageCount > 0)
                    {
                        // Set DPI for high-quality images
                        f.ImageOptions.Dpi = 300;
                    }

                    double maxImageCount = 25;
                    int maxSize = (int)Math.Ceiling(f.PageCount / maxImageCount);
                    var pageImageFiles = new List<string>();
                    int count = 0;

                    for (int pageIndex = 0; pageIndex < f.PageCount; pageIndex += maxSize)
                    {
                        var pageImageGroup = new List<SKBitmap>();
                        int pageCountToConvert = Math.Min(maxSize, f.PageCount - pageIndex);

                        for (int i = 0; i < pageCountToConvert; i++)
                        {
                            f.ImageOptions.PageIndex = pageIndex + i;
                            string tempPngFilePath = Path.Combine(Path.GetTempPath(), $"{pdfName}.Part_{count}_{i}.png");
                            if (f.ToImage(tempPngFilePath) == 0)
                            {
                                using (var stream = new FileStream(tempPngFilePath, FileMode.Open, FileAccess.Read))
                                {
                                    pageImageGroup.Add(SKBitmap.Decode(stream));
                                }
                            }
                        }

                        if (pageImageGroup.Count > 0)
                        {
                            var pdfImageName = $"{pdfName}.Part_{count}.png"; // Change file extension to .png

                            int totalHeight = pageImageGroup.Sum(image => image.Height);
                            int width = pageImageGroup.Max(image => image.Width);
                            var stitchedImage = new SKBitmap(width, totalHeight);
                            var canvas = new SKCanvas(stitchedImage);
                            int currentHeight = 0;
                            foreach (var pageImage in pageImageGroup)
                            {
                                canvas.DrawBitmap(pageImage, 0, currentHeight);
                                currentHeight += pageImage.Height;
                            }

                            using (var stitchedFileStream = new FileStream(pdfImageName, FileMode.Create, FileAccess.Write))
                            {
                                stitchedImage.Encode(stitchedFileStream, SKEncodedImageFormat.Png, 100); // Change to Png format
                            }
                            pageImageFiles.Add(pdfImageName);
                            count++;

                            Console.WriteLine($"Saved image to {pdfImageName}");
                        }
                    }

                    var userPromptParts = new List<JsonNode>
                    {
                        new JsonObject
                        {
                            { "type", "text" },
                            { "text", $"Extract the data from this document. If a value is not present, provide null. Use the following structure: {JsonSerializer.Serialize(Metadata.Empty)}" }
                        }
                    };

                    foreach (var pdfImageFile in pageImageFiles)
                    {
                        var imageBytes = await File.ReadAllBytesAsync(pdfImageFile);
                        var base64Image = Convert.ToBase64String(imageBytes);
                        userPromptParts.Add(new JsonObject
                        {
                            { "type", "image_url" },
                            { "image_url", new JsonObject 
                                { 
                                    { "url", $"data:image/png;base64,{base64Image}" }, // Change MIME type to image/png
                                    { "detail", "high" } // Adding the detail property
                                } 
                            }
                        });
                    }

                    JsonObject jsonPayload = new JsonObject
                    {
                        {
                            "messages", new JsonArray 
                            {
                                new JsonObject
                                {
                                    { "role", "system" },
                                    { "content", systemPrompt }
                                },
                                new JsonObject
                                {
                                    { "role", "user" },
                                    { "content", new JsonArray(userPromptParts.ToArray()) }
                                }
                            }
                        },
                        { "model", modelDeployment },
                        { "max_tokens", 4096 },
                        { "temperature", 0.1 },
                        { "top_p", 0.1 },
                    };

                    string payload = JsonSerializer.Serialize(jsonPayload, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                    var metadata = Metadata.Empty;

                    using (HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
                    {
                        // Set the base address without query parameters
                        httpClient.BaseAddress = new Uri(baseUri);
                        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {bearerToken}");
                        httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                        var stringContent = new StringContent(payload, Encoding.UTF8, "application/json");

                        // Append query parameters to the request URI
                        var requestUri = new Uri($"{baseUri}?api-version={apiVersion}");

                        var response = await httpClient.PostAsync(requestUri, stringContent);

                        if (response.IsSuccessStatusCode)
                        {
                            File.WriteAllText(Path.Combine(outputFolderPath, responseFileName), await response.Content.ReadAsStringAsync());

                            using (var responseStream = await response.Content.ReadAsStreamAsync())
                            {
                                // Parse the JSON response using JsonDocument
                                using (var jsonDoc = await JsonDocument.ParseAsync(responseStream))
                                {
                                    // Access the message content dynamically
                                    JsonElement jsonElement = jsonDoc.RootElement;

                                    if (jsonElement.TryGetProperty("choices", out JsonElement choices) &&
                                        choices.GetArrayLength() > 0 &&
                                        choices[0].TryGetProperty("message", out JsonElement message) &&
                                        message.TryGetProperty("content", out JsonElement content))
                                    {
                                        string messageContent = content.GetString();

                                        // Clean up the message content
                                        string cleanedMessageContent = messageContent.Trim(new[] { ' ', '\n', '\r' });

                                        // Remove "json" prefix if present
                                        if (cleanedMessageContent.StartsWith("```json"))
                                        {
                                            cleanedMessageContent = cleanedMessageContent.Substring(7).Trim();
                                        }
                                        if (cleanedMessageContent.EndsWith("```"))
                                        {
                                            cleanedMessageContent = cleanedMessageContent.Substring(0, cleanedMessageContent.Length - 3).Trim();
                                        }

                                        // Validate JSON structure
                                        if (cleanedMessageContent.StartsWith("{") && cleanedMessageContent.EndsWith("}"))
                                        {
                                            try
                                            {
                                                // Parse the JSON content into a dictionary
                                                var root = JsonSerializer.Deserialize<Dictionary<string, object>>(cleanedMessageContent);

                                                // Create a new dictionary to ensure FileName is first
                                                var modifiedRoot = new Dictionary<string, object>
                                                {
                                                    { "FileName", pdfName }
                                                };

                                                // Add all other properties from the original root
                                                foreach (var kvp in root)
                                                {
                                                    modifiedRoot[kvp.Key] = kvp.Value;
                                                }

                                                // Serialize back to a JSON string with indentation
                                                var options = new JsonSerializerOptions { WriteIndented = true };
                                                string modifiedJson = JsonSerializer.Serialize(modifiedRoot, options);

                                                // Write the modified JSON to file
                                                File.WriteAllText(Path.Combine(outputFolderPath, pdfJsonExtractionName), modifiedJson);

                                                Console.WriteLine($"{Path.Combine(outputFolderPath, pdfJsonExtractionName)} has been created with the content from the response from the OpenAI API.");
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine($"Error modifying JSON: {ex.Message}");
                                                // Write the original cleaned content if modification fails
                                                File.WriteAllText(Path.Combine(outputFolderPath, pdfJsonExtractionName), cleanedMessageContent);
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine("Invalid JSON structure in response.");
                                            // Write the original cleaned content if it's not a valid JSON object
                                            File.WriteAllText(Path.Combine(outputFolderPath, pdfJsonExtractionName), cleanedMessageContent);
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("Unexpected JSON structure in response from API.");
                                        // Write an empty metadata object if the expected structure is not found
                                        var options = new JsonSerializerOptions { WriteIndented = true };
                                        string emptyJson = JsonSerializer.Serialize(Metadata.Empty, options);
                                        File.WriteAllText(Path.Combine(outputFolderPath, pdfJsonExtractionName), emptyJson);
                                    }
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine(await response.Content.ReadAsStringAsync());
                            // Write an empty metadata object if the API request fails
                            var options = new JsonSerializerOptions { WriteIndented = true };
                            string emptyJson = JsonSerializer.Serialize(Metadata.Empty, options);
                            File.WriteAllText(Path.Combine(outputFolderPath, pdfJsonExtractionName), emptyJson);
                        }
                    }

                    // Clean up generated images
                    foreach (var imageFile in pageImageFiles)
                    {
                        File.Delete(imageFile);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }

        public class Metadata
        {
            public class MetadataItem
            {
                public string? Reasoning { get; set; }
                public string? Citation { get; set; }
                public double Confidence { get; set; }
                public string? Value { get; set; }
            }

            public MetadataItem? DocumentTitle { get; set; }
            public MetadataItem? DateOfDocument { get; set; }
            public MetadataItem? DocumentRevision { get; set; }
            public MetadataItem? DocumentType { get; set; }
            public MetadataItem? DocumentType2 { get; set; }
            public MetadataItem? DocumentType3 { get; set; }
            public MetadataItem? Discipline { get; set; }
            public MetadataItem? Discipline2 { get; set; }
            public MetadataItem? Discipline3 { get; set; }
            public MetadataItem? LegacyNumber { get; set; }
            public MetadataItem? Equipment { get; set; }
            public MetadataItem? SubEquipment { get; set; }
            public MetadataItem? TagNumber { get; set; }
            public MetadataItem? ProjectID_AFE { get; set; }
            public MetadataItem? FacilityCode { get; set; }
            public MetadataItem? ThirdPartyName { get; set; }

            public static Metadata Empty => new Metadata
            {
                DocumentTitle = new MetadataItem { Reasoning = null, Citation = null, Confidence = 0, Value = null },
                DateOfDocument = new MetadataItem { Reasoning = null, Citation = null, Confidence = 0, Value = null },
                DocumentRevision = new MetadataItem { Reasoning = null, Citation = null, Confidence = 0, Value = null },
                DocumentType = new MetadataItem { Reasoning = null, Citation = null, Confidence = 0, Value = null },
                DocumentType2 = new MetadataItem { Reasoning = null, Citation = null, Confidence = 0, Value = null },
                DocumentType3 = new MetadataItem { Reasoning = null, Citation = null, Confidence = 0, Value = null },
                Discipline = new MetadataItem { Reasoning = null, Citation = null, Confidence = 0, Value = null },
                Discipline2 = new MetadataItem { Reasoning = null, Citation = null, Confidence = 0, Value = null },
                Discipline3 = new MetadataItem { Reasoning = null, Citation = null, Confidence = 0, Value = null },
                LegacyNumber = new MetadataItem { Reasoning = null, Citation = null, Confidence = 0, Value = null },
                Equipment = new MetadataItem { Reasoning = null, Citation = null, Confidence = 0, Value = null },
                SubEquipment = new MetadataItem { Reasoning = null, Citation = null, Confidence = 0, Value = null },
                TagNumber = new MetadataItem { Reasoning = null, Citation = null, Confidence = 0, Value = null },
                ProjectID_AFE = new MetadataItem { Reasoning = null, Citation = null, Confidence = 0, Value = null },
                FacilityCode = new MetadataItem { Reasoning = null, Citation = null, Confidence = 0, Value = null },
                ThirdPartyName = new MetadataItem { Reasoning = null, Citation = null, Confidence = 0, Value = null }
            };
        }
    }
}