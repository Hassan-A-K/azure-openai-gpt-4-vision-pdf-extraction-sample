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
                            { "text", $"Extract the data from this invoice. If a value is not present, provide null. Reasons may overlap multiple lines, arrows indicate which reason relates to which line item. Use the following structure:{JsonSerializer.Serialize(InvoiceData.Empty)}" }
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

                    var invoiceData = InvoiceData.Empty;

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
                                            invoiceData = JsonSerializer.Deserialize<InvoiceData>(messageContent);
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

        public class InvoiceData
        {
            public string? InvoiceNumber { get; set; }
            public string? PurchaseOrderNumber { get; set; }
            public string? CustomerName { get; set; }
            public string? CustomerAddress { get; set; }
            public DateTime? DeliveryDate { get; set; }
            public DateTime? PayableBy { get; set; }
            public IEnumerable<InvoiceDataProduct>? Products { get; set; }
            public IEnumerable<InvoiceDataProduct>? Returns { get; set; }
            public double? TotalQuantity { get; set; }
            public double? TotalPrice { get; set; }
            public IEnumerable<InvoiceDataSignature>? ProductsSignatures { get; set; }
            public IEnumerable<InvoiceDataSignature>? ReturnsSignatures { get; set; }

            public static InvoiceData Empty => new()
            {
                InvoiceNumber = string.Empty,
                PurchaseOrderNumber = string.Empty,
                CustomerName = string.Empty,
                CustomerAddress = string.Empty,
                DeliveryDate = DateTime.MinValue,
                Products =
                    new List<InvoiceDataProduct> { new() { Id = string.Empty, Description = string.Empty, UnitPrice = 0.0, Quantity = 0.0, Total = 0.0 } },
                Returns =
                    new List<InvoiceDataProduct> { new() { Id = string.Empty, Quantity = 0.0, Reason = string.Empty } },
                TotalQuantity = 0,
                TotalPrice = 0,
                ProductsSignatures = new List<InvoiceDataSignature>
                {
                    new()
                    {
                        Type = string.Empty,
                        Name = string.Empty,
                        IsSigned = false
                    }
                },
                ReturnsSignatures = new List<InvoiceDataSignature>
                {
                    new()
                    {
                        Type = string.Empty,
                        Name = string.Empty,
                        IsSigned = false
                    }
                }
            };

            public class InvoiceDataProduct
            {
                public string? Id { get; set; }
                public string? Description { get; set; }
                public double? UnitPrice { get; set; }
                public double Quantity { get; set; }
                public double? Total { get; set; }
                public string? Reason { get; set; }
            }

            public class InvoiceDataSignature
            {
                public string? Type { get; set; }
                public string? Name { get; set; }
                public bool? IsSigned { get; set; }
            }
        }
    }
}
