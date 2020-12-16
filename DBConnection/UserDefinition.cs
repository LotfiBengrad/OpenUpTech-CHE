namespace server.DBConnection
{
    using System;
    using System.Collections.Generic;
    using MongoDB.Bson.Serialization;
    using MongoDB.Bson;
    using MongoDB.Bson.Serialization.Attributes;
    using MongoDB.Bson.Serialization.Options;
    using OpenUp.Networking.ServerCalls;

    /// <summary>
    /// This contains the data for a user.
    /// </summary>
    /// <remarks>
    /// Everything is defined as a class as the MongoDB driver cannot access structs
    /// in nested statements in queries. This behaviour is not accessible from outside
    /// the library and cannot be fixed. <sadface/>
    /// </remarks>
    [BsonIgnoreExtraElements]
    public class User
    {
        [BsonId]
        public string id { get; set; }

        [BsonElement("username")]
        public string username { get; set; }

        [BsonElement("services")]
        public Services services { get; set; }

        [BsonElement("emails")]
        public List<Email> emails { get; set; }
        
        [BsonIgnoreExtraElements]
        public class Services
        {
            [BsonElement("password")]
            public Password password { get; set; }

            [BsonElement("resume")]
            public Resume resume { get; set; }
        }

        [BsonIgnoreExtraElements]
        public class Email
        {
            [BsonElement("address")]
            public string address { get; set; }

            [BsonElement("verified")]
            public bool verified { get; set; }
        }

        [BsonIgnoreExtraElements]
        public class Password
        {
            [BsonElement("bcrypt")]
            public string bcrypt { get; set; }
        }
    
        [BsonIgnoreExtraElements]
        public class Resume
        {
            [BsonElement("loginTokens")]
            public List<ResumeToken> loginTokens { get; set; }
        }
    
        [BsonIgnoreExtraElements]
        public class ResumeToken
        {
            [BsonElement("token")]
            public string token { get; set; }
        
            [BsonElement("expires")]
            public DateTime expires { get; set; }
        }

        internal static void AddMappings()
        {
            BsonClassMap.RegisterClassMap<User>(
                cm =>
                {
                    cm.AutoMap();
                    
                    // This method must take the fields as separate arguments due to MapCreator restrictions
                    Func<string, string, Services, List<Email>, Profile, User> insertUser = (
                        id,
                        username,
                        services,
                        emails,
                        profile
                    ) =>
                    {
                        User u = new User
                        {
                            id       = id,
                            username = username,
                            services = services,
                            emails = emails,
                            profile = profile
                        };
                        
                        profile.assets?.ForEach(ass => ass.authorName = username);
                        
                        return u;
                    };
                    
                    // MapCreator method throws an error if the lambda does anything with its argument other than 
                    // access properties and fields (it will even error if the argument is passed directly to another function)
                    cm.MapCreator(user => insertUser(user.id, user.username, user.services, user.emails, user.profile));
                }
            );

            BsonClassMap.RegisterClassMap<Profile>(
                cm =>
                {
                    cm.AutoMap();
                    cm.MapMember(c => c.assets).SetDefaultValue(new List<Asset>());
                    cm.MapMember(c => c.recentAssets).SetDefaultValue(new Dictionary<string, RecentAssets>());
                }
            );
        }

        [BsonElement("profile")]
        public Profile profile { get; set; }

        [BsonIgnoreExtraElements]
        public class Profile
        {
            [BsonElement("assets")]
            public List<Asset> assets { get; set; }

            [BsonElement("recentAssets")]
            [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
            public Dictionary<string, RecentAssets> recentAssets { get; set; }
        }

        public override string ToString()
        {
            return $"{username} (id: {id})";
        }
    }
}
