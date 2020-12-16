namespace server.Utils
{
    using MongoDB.Bson;
    using MongoDB.Bson.IO;
    using System;

    public static class BsonUtils
    {

        public static void LeaveDocument(this IBsonReader reader)
        {
            while (reader.ReadBsonType() != BsonType.EndOfDocument)
            {
                string field = reader.ReadName();
                reader.SkipValue();
                   
                if (reader.IsAtEndOfFile()) // end of the collection
                {
                    Console.WriteLine("EOF");
                    break;
                }
            }
            reader.ReadEndDocument();
        }
    }
}