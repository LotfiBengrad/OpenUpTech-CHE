namespace server.RestAPI
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;
    using DBConnection;
    using Microsoft.AspNetCore.Cors;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Primitives;
    using MongoDB.Driver;
    using OpenUp.Networking.ServerCalls;

    [ApiController]
    public class AssetController : ControllerBase
    {
        [Route("api/users/{userID}/assets")]
        [EnableCors(Startup.CORS_POLICY)]
        [HttpGet]
        public async Task<List<Asset>> GetAssets(string userID)
        {
            Request.Headers.TryGetValue(UserController.AUTH_KEY_NAME, out StringValues authKey);

            bool isValid = await MongoConnection.Instance.IsValidToken(userID, authKey);
            
            User user = await MongoConnection.Instance.GetUser(userID);

            return user.profile.assets;
        }
        
        [Route("api/users/{userID}/assets/{assetID}")]
        [EnableCors(Startup.CORS_POLICY)]
        [HttpGet]
        public async Task<Asset> GetAsset(string userID, string assetID)
        {
            User user = await MongoConnection.Instance.GetUser(userID);

            return user.profile.assets
                       .FirstOrDefault(ass => ass.id == assetID);
        }
        
        [Route("api/users/{userID}/assets")]
        [EnableCors(Startup.CORS_POLICY)]
        [HttpPost]
        public async Task<string> PostAsset(string userID, Asset asset)
        {
            try
            {
                User user = await MongoConnection.Instance.GetUser(userID);
                
                FilterDefinition<User> filter = Builders<User>.Filter.Eq("_id", user.id);
                UpdateDefinition<User> update = Builders<User>.Update.Push("profile.assets", asset);
            
                await MongoConnection.Instance.users.FindOneAndUpdateAsync(filter, update);

                return JsonSerializer.Serialize(asset);
            }
            catch (Exception exception)
            {
                return JsonSerializer.Serialize(exception);
            }
        }
    }
}