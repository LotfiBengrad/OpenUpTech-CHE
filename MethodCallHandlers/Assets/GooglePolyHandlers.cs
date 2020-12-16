namespace server
{
    using DBConnection;
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using OpenUp.Networking.ServerCalls;

    // Use of System.Text.Json requires .net core 3.0 or higher
    using System.Text.Json;
    
    public partial class MethodCallHandlers : IServerCallMethods
    {
        private const string POLY_URL = "https://poly.googleapis.com/v1/assets";
        
        // Test User api Key
        private const string POLY_APIKEY = "AIzaSyAlCxnJPdjgtTirEu45bjxV5arImpEA_ec";

        public async Task<PolyModelList> GetGooglePolyModels(string searchTerm, int count = 10, string pageToken = null)
        {
            // Google Poly API only accepts requests for up to 100 models at once
            count = Math.Min(count, 100);

            Console.WriteLine($"The get poly models thing was called searching for '{searchTerm}', with pageSize {count}.");
            if (pageToken != null)
                Console.WriteLine("Requested pageToken: " + pageToken);

            // TODO: check what happens when I use two search keywords with a space
            HttpClient client = new HttpClient();
            string pageTokenRequest = pageToken == null ? "" : $"&pageToken={pageToken}";
            string requestURL = $"{POLY_URL}?format=GLTF&keywords={searchTerm}{pageTokenRequest}&pageSize={count}&key={POLY_APIKEY}";
            string responseBody = await client.GetStringAsync(requestURL);

            List<Asset> modelList = new List<Asset>(count);
            string nextPageToken = null;

            // Parse JSON reply
            using (JsonDocument document = JsonDocument.Parse(responseBody))
            {
                // Check if document has an assets array (no results means an empty JSON object gets returned)
                JsonElement assets;
                if (document.RootElement.TryGetProperty("assets", out assets))
                {
                    try
                    {
                        // int idx = 0;
                        // Process assets
                        foreach (JsonElement element in assets.EnumerateArray())
                        {
                            // Get GLTF model URL
                            string mainModelFile = "";

                            foreach (JsonElement format in element.GetProperty("formats").EnumerateArray())
                            {
                                // Skip non-GLTF(2) formats
                                string formatType = format.GetProperty("formatType").GetString();
                            
                                if (formatType != "GLTF2") continue;

                                // Proper format found, retrieve URL
                                mainModelFile = format.GetProperty("root").GetProperty("url").GetString();
                                break;
                            }

                            if (String.IsNullOrEmpty(mainModelFile)) continue;

                            // PolyModel m = new PolyModel
                            Asset m = new Asset
                                      {
                                          id            = element.GetProperty("name").GetString(),
                                          name          = element.GetProperty("displayName").GetString(),
                                          type          = "model",
                                          authorName    = element.GetProperty("authorName").GetString(),
                                          mainModelFile = mainModelFile,
                                          thumbnail     = element.GetProperty("thumbnail").GetProperty("url").GetString()
                                      };
                        
                            // Add model to list
                            modelList.Add(m);
                        }
                    }
                    catch (Exception exception)
                    {
                        // don't break on invalid data from api
                    }

                    // Get token for next page of results (key won't exist if there are no remaining results)
                    JsonElement nextPageTokenElement;
                    if (document.RootElement.TryGetProperty("nextPageToken", out nextPageTokenElement))
                        nextPageToken = nextPageTokenElement.GetString();
                }
            }

            return new PolyModelList {
                nextPageToken = nextPageToken,
                modelList = modelList
            };
        }
    }
}
