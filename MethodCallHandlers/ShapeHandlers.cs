namespace server
{
    using DBConnection;
    using MongoDB.Bson;
    using MongoDB.Driver;
    using OpenUp.DataStructures;
    using OpenUp.DataStructures.SceneShots;
    using OpenUp.Networking.ServerCalls;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Utils;

    public partial class MethodCallHandlers : IServerCallMethods
    {
        public async Task<List<ShapeRef>> GetAllShapes()
        {
            IAsyncCursor<ShapeRef> shapes = await MongoConnection.Instance.shapes.FindAsync<ShapeRef>(new BsonDocument());

            return shapes.ToList();
        }

        public Task<Exception> UpdateSceneShot(string id, SceneShot sceneShot)
        {
            throw new NotImplementedException();
        }

        public async Task UpdateShape(ShapeRef newShape)
        {
            long count = await MongoConnection.Instance.shapes.CountDocumentsAsync(shp => shp.id == newShape.id);
            
            if (count == 0)
            {
                await MongoConnection.Instance.shapes.InsertOneAsync(newShape);
            }
            else
            {
                await MongoConnection.Instance.shapes.ReplaceOneAsync(shp => shp.id == newShape.id, newShape);
            }
            
            DBUtils.UseShapeRef(newShape);
        }
    }
}