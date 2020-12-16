namespace server
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DBConnection;
    using MongoDB.Bson;
    using MongoDB.Driver;
    using OpenUp.Networking.ServerCalls;
    using OpenUp.DataStructures.SceneShots;

    public partial class MethodCallHandlers : IServerCallMethods
    {
        public async Task<Exception> AddSceneShot(SceneShot sceneShot)
        {
            Console.WriteLine($"Adding a scene shot for app `{sceneShot.appID}`");

            try
            {
                // Upserts the replacement, meaning it inserts instead if the filter has no match
                await MongoConnection.Instance.sceneShots.ReplaceOneAsync(
                    s => s.appID == sceneShot.appID,
                    sceneShot,
                    new ReplaceOptions {
                        IsUpsert = true
                    }
                );
                return null;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Exception occured - {exception.GetType()}: {exception.Message}");
                Console.WriteLine(exception.StackTrace);
                return exception;
            }
        }

        public async Task<Exception> UpdateSceneShot(ObjectId id, SceneShot sceneShot)
        {
            try
            {
                long c = await MongoConnection.Instance.sceneShots.CountDocumentsAsync(s => s.id == id);

                if (c == 0)
                    throw new ArgumentException($"No scene shot found with id `{id}` to update.");
                else
                    await MongoConnection.Instance.sceneShots.ReplaceOneAsync(s => s.id == id, sceneShot);

                return null;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Exception occured - {exception.GetType()}: {exception.Message}");
                Console.WriteLine(exception.StackTrace);
                return exception;
            }
        }

        public async Task<List<SceneShot>> FetchSceneShots(string appID)
        {
            return MongoConnection.Instance.sceneShots.Find(s => s.appID == appID).ToList();
        }

        public async Task<Exception> DeleteSceneShot(ObjectId id)
        {
            try
            {
                long c = await MongoConnection.Instance.sceneShots.CountDocumentsAsync(s => s.id == id);

                if (c == 0)
                    throw new ArgumentException($"No scene shot found with id `{id}` to delete.");
                else
                    await MongoConnection.Instance.sceneShots.DeleteOneAsync(s => s.id == id);

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
