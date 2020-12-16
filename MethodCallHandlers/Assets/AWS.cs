namespace server
{
    using System;
    using System.Threading.Tasks;
    using OpenUp.Networking.ServerCalls;
    using Amazon;
    using Amazon.S3;
    using Amazon.S3.Model;

    public partial class MethodCallHandlers : IServerCallMethods
    {
        // Uses the AWS_ACCESS_KEY_ID and AWS_SECRET_ACCESS_KEY system environment variables for authentication
        private AmazonS3Client S3Client = new AmazonS3Client(RegionEndpoint.EUCentral1);

        public async Task<string> GetS3SigningURL(string key, string type)
        {
            try
            {
                // Generate signed URL using AWS SDK
                GetPreSignedUrlRequest request = new GetPreSignedUrlRequest
                {
                    BucketName  = "openuptech-assets",
                    Key         = key,
                    Verb        = HttpVerb.PUT,
                    ContentType = type,
                    Expires     = DateTime.Now.AddMinutes(5),
                    Headers     =
                    {
                        ["x-amz-acl"] = "public-read"
                    }
                };
                string signedURL = S3Client.GetPreSignedURL(request);

                // Return generated signed URL
                Console.WriteLine($"Generated signed url for '{key}' of type '{type}': {signedURL}");
                return signedURL;
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}
