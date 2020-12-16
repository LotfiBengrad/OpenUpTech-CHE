namespace server
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using MongoDB.Driver;
    using DBConnection;
    using OpenUp.Networking.ServerCalls;

    public partial class MethodCallHandlers : IServerCallMethods
    {
        public async Task<List<Asset>> ListMyAssets(string searchTerm = null)
        {
            User user = await MongoConnection.Instance.GetUser(connection.user.name);

            List<Asset> assetList = user.profile.assets;

            if (searchTerm == null)
                return assetList;
            else
                return assetList.FindAll(asset => asset.name.Contains(searchTerm, StringComparison.CurrentCultureIgnoreCase));
        }

        public async Task<Exception> AddMyAsset(Asset asset)
        {
            try
            {
                User user = await MongoConnection.Instance.GetUser(connection.user.name);
                
                FilterDefinition<User> filter = Builders<User>.Filter.Eq("_id", user.id);
                UpdateDefinition<User> update = Builders<User>.Update.Push("profile.assets", asset);
                await MongoConnection.Instance.users.FindOneAndUpdateAsync(filter, update);

                Console.WriteLine($"Inserted asset {asset.id} into {user.username}'s profile.assets");

                return null;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Exception occured - {exception.GetType()}: {exception.Message}");
                Console.WriteLine(exception.StackTrace);
                return exception;
            }
        }

        public async Task<Exception> DeleteMyAsset(string assetID)
        {
            try
            {
                User user = await MongoConnection.Instance.GetUser(connection.user.name);
                Console.WriteLine($"Removing asset {assetID} from user '{user.username}'");

                // Test if asset exist
                FilterDefinition<User> testFilter = Builders<User>.Filter.And(
                    Builders<User>.Filter.Eq("_id", user.id),
                    Builders<User>.Filter.Eq("profile.assets.id", assetID)
                );
                List<User> testResult = MongoConnection.Instance.users.Find(testFilter).ToList();

                if (testResult.Count == 0)
                    throw new ArgumentException($"User '{user.username}' does not have an asset with id '{assetID}'");

                // Delete specified asset
                FilterDefinition<User> filter = Builders<User>.Filter.Eq("_id", user.id);
                UpdateDefinition<User> update = Builders<User>.Update.PullFilter(
                    "profile.assets",
                    Builders<Asset>.Filter.Eq("id", assetID)
                );
                await MongoConnection.Instance.users.FindOneAndUpdateAsync(filter, update);

                return null;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Exception occured - {exception.GetType()}: {exception.Message}");
                Console.WriteLine(exception.StackTrace);
                return exception;
            }
        }

        public async Task<List<Asset>> ListRecentAssets(string searchTerm = null)
        {
            User user = await MongoConnection.Instance.GetUser(connection.user.name);

            // Return empty list if user doesn't have any recent assets for the current app
            if (!user.profile.recentAssets.ContainsKey(connection.session.app.id))
                return new List<Asset>();

            List<Asset> assetList = user.profile.recentAssets[connection.session.app.id].usedAssets;

            // Filter result on searchTerm if it is provided
            if (searchTerm != null)
                assetList = assetList.FindAll(asset => asset.name.Contains(searchTerm, StringComparison.CurrentCultureIgnoreCase));

            return assetList;
        }

        public async Task<Exception> AddRecentAsset(Asset asset)
        {
            try
            {
                User user = await MongoConnection.Instance.GetUser(connection.user.name);
                string appID = connection.session.app.id;

                // Create new app in recentAssets if current app doesn't exist the dictionary yet
                if (!user.profile.recentAssets.ContainsKey(appID))
                {
                    user.profile.recentAssets.Add(appID, new RecentAssets {
                        usedAssets = new List<Asset>()
                    });
                    Console.WriteLine($"New app added to {user.username}'s recentAssets: {appID}");
                }
                
                // Update lastUpdated timestamp
                user.profile.recentAssets[appID].lastUpdated = DateTime.Now;

                // Remove asset from list if it already exists, so it'll get reinserted at the top
                int assetIndex = user.profile.recentAssets[appID].usedAssets.FindIndex(ass => ass.id == asset.id);
                if (assetIndex != -1)
                    user.profile.recentAssets[appID].usedAssets.RemoveAt(assetIndex);

                // Insert new asset at the start of the assets list
                user.profile.recentAssets[appID].usedAssets.Insert(0, asset);

                // Limit list size to most recent (first X) only
                int maxListSize = 10;
                if (user.profile.recentAssets[appID].usedAssets.Count > maxListSize)
                    user.profile.recentAssets[appID].usedAssets = user.profile.recentAssets[appID].usedAssets.GetRange(0, maxListSize);

                // Write new assets list to the database
                FilterDefinition<User> filter = Builders<User>.Filter.Eq("_id", user.id);
                UpdateDefinition<User> update = Builders<User>.Update.Set(
                    $"profile.recentAssets",
                    user.profile.recentAssets
                );
                await MongoConnection.Instance.users.FindOneAndUpdateAsync(filter, update);

                Console.WriteLine($"Inserted asset {asset.id} into {user.username}'s profile.recentAssets for app {appID}");

                return null;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Exception occured - {exception.GetType()}: {exception.Message}");
                Console.WriteLine(exception.StackTrace);
                return exception;
            }
        }
    }
}
