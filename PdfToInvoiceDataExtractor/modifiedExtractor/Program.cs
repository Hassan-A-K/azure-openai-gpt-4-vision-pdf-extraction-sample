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
using PDFtoImage;
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
            var apiVersion = "2024-03-01-preview";

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
                string diagramsFolderPath = Path.Combine(demoFilesPath, "Diagrams");

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

                string systemPromptFilePath = Path.Combine(currentDirectory, "DCPromptsForAzureOpenAI.txt");
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

                    var pdf = await File.ReadAllBytesAsync(pdfFilePath);
                    var pageImages = PDFtoImage.Conversion.ToImages(pdf);

                    double maxImageCount = 25;
                    int maxSize = (int)Math.Ceiling(pageImages.Count() / maxImageCount);
                    var pageImageGroups = new List<List<SKBitmap>>();
                    for (int i = 0; i < pageImages.Count(); i += maxSize)
                    {
                        var pageImageGroup = pageImages.Skip(i).Take(maxSize).ToList();
                        pageImageGroups.Add(pageImageGroup);
                    }

                    var pdfImageFiles = new List<string>();

                    var count = 0;
                    foreach (var pageImageGroup in pageImageGroups)
                    {
                        var pdfImageName = $"{pdfName}.Part_{count}.jpg";

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
                            stitchedImage.Encode(stitchedFileStream, SKEncodedImageFormat.Jpeg, 100);
                        }
                        pdfImageFiles.Add(pdfImageName);
                        count++;

                        Console.WriteLine($"Saved image to {pdfImageName}");
                    }

                    var userPromptParts = new List<JsonNode>{
                        new JsonObject
                        {
                            { "type", "text" },
                            { "text", $"Extract the data from this document. If a value is not present, provide null. Use the following structure:{JsonSerializer.Serialize(Metadata.Empty)}" }
                        }
                    };

                    foreach (var pdfImageFile in pdfImageFiles)
                    {
                        var imageBytes = await File.ReadAllBytesAsync(pdfImageFile);
                        var base64Image = Convert.ToBase64String(imageBytes);
                        userPromptParts.Add(new JsonObject
                        {
                            { "type", "image_url" },
                            { "image_url", new JsonObject { { "url", $"data:image/jpeg;base64,{base64Image}" } } }
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
                                    { "content", new JsonArray(userPromptParts.ToArray())}
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

                                        // Output the message content
                                        File.WriteAllText(Path.Combine(outputFolderPath, pdfJsonExtractionName), messageContent);
                                        Console.WriteLine($"{Path.Combine(outputFolderPath, pdfJsonExtractionName)} has been created with the content from the response from the OpenAI API.");

                                        if (messageContent != null)
                                        {
                                            metadata = JsonSerializer.Deserialize<Metadata>(messageContent);
                                        }
                                        else
                                        {
                                            Console.WriteLine("Received null response content from the API.");
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("Unexpected JSON structure in response from API.");
                                    }
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine(await response.Content.ReadAsStringAsync());
                        }
                    }

                    // Clean up generated images
                    foreach (var imageFile in pdfImageFiles)
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
            public string? FileName { get; set; }
            public string? DocumentTitle { get; set; }
            public DateTime? DateOfDocument { get; set; }
            public string? DocumentRevision { get; set; }
            public string? DocumentType { get; set; }
            public string? Discipline { get; set; }
            public string? LegacyNumber { get; set; }
            public string? Equipment { get; set; }
            public string? SubEquipment { get; set; }
            public string? TagNumber { get; set; }
            public string? ProjectIDAFENumberFromFolder { get; set; }
            public string? FacilityCode { get; set; }
            public string? ThirdPartyName { get; set; }

            public static Metadata Empty => new()
            {
                FileName = string.Empty,
                DocumentTitle = string.Empty,
                DateOfDocument = null,
                DocumentRevision = string.Empty,
                DocumentType = string.Empty,
                Discipline = string.Empty,
                LegacyNumber = string.Empty,
                Equipment = string.Empty,
                SubEquipment = string.Empty,
                TagNumber = string.Empty,
                ProjectIDAFENumberFromFolder = string.Empty,
                FacilityCode = string.Empty,
                ThirdPartyName = string.Empty
            };
        }
    }
}