namespace server
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DBConnection;
    using MongoDB.Bson;
    using MongoDB.Driver;
    using OpenUp.Networking.ServerCalls;
    using OpenUp.DataStructures.LDraw;

    public partial class MethodCallHandlers : IServerCallMethods
    {
        public async Task<Exception> LDraw_AddStandardModel(LDrawModel model)
        {
            try
            {
                long c = await MongoConnection.Instance.LDrawStandardModels.CountDocumentsAsync(m => m.id == model.id);

                if (c == 0)
                {
                    await MongoConnection.Instance.LDrawStandardModels.InsertOneAsync(model);
                }
                else
                {
                    await MongoConnection.Instance.LDrawStandardModels.ReplaceOneAsync(m => m.id == model.id, model);
                }

                return null;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Exception occured - {exception.GetType()}: {exception.Message}");
                Console.WriteLine(exception.StackTrace);
                return exception;
            }
        }

        public async Task<List<LDrawModel>> LDraw_FetchStandardModel(SearchOptions options)
        {
            List<FilterDefinition<LDrawModel>> filters = new List<FilterDefinition<LDrawModel>>();

            // Name filter
            if (options.name != null && options.name != "")
            {
                filters.Add(
                    Builders<LDrawModel>.Filter.Text(options.name, new TextSearchOptions { CaseSensitive = false }
                ));
            }

            // Category filter
            if (options.category != SearchOptions.Categories.ANY)
            {
                // Match all models with a category field of NULL
                if (options.category == SearchOptions.Categories.NONE)
                    filters.Add(Builders<LDrawModel>.Filter.Type("category", BsonType.Null));

                // Normal match of category
                else
                {
                    filters.Add(
                        Builders<LDrawModel>.Filter.Eq("category", options.category.ToString().Replace("_", " "))
                    );
                }
            }

            // Keyword filter
            if (options.keywords.Length > 0)
            {
                filters.Add(
                    Builders<LDrawModel>.Filter.All("keywords", options.keywords)
                );
            }

            // Construct final filter out of filters defined above
            FilterDefinition<LDrawModel> finalFilter;
            if (filters.Count == 0)
                finalFilter = Builders<LDrawModel>.Filter.Empty;
            else
            {
                finalFilter = options.matchAllOptions == true ?
                              Builders<LDrawModel>.Filter.And(filters) :
                              Builders<LDrawModel>.Filter.Or(filters);
            }

            // Make database request
            List<LDrawModel> modelList = MongoConnection.Instance.LDrawStandardModels.Find(finalFilter)
                                                                                     .Limit(options.maxCount)
                                                                                     .ToList();

            return modelList;
        }

        public async Task<LDrawModel> LDraw_FetchStandardModelByID(string ID)
        {
            List<LDrawModel> modelList = MongoConnection.Instance.LDrawStandardModels.Find(m => m.id == ID).ToList();

            if (modelList.Count > 0)
                return modelList[0];
            else
                throw new System.IO.FileNotFoundException($"Could not find model with id `{ID}`");
        }
    }
}
