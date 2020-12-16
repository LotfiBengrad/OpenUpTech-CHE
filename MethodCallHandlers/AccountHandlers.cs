namespace server
{
    using DBConnection;
    using OpenUp.Networking.ServerCalls;
    using System;
    using System.Threading.Tasks;

    public class LoginException : Exception
    {
        public readonly string message;
        public readonly string details;
        
        public LoginException(string message, string details) : base(message)
        {
            this.message = message;
            this.details = details;
        }
        public static LoginException UnknownUser(string username)
        {
            return new LoginException("Unknown Username", $"The user {username} is not known");
        }
        
        public static LoginException PasswordIncorrect()
        {
            return new LoginException("Invalid Credentials", $"Password was incorrect");
        }
        
        public static LoginException InvalidToken()
        {
            return new LoginException("Invalid Token", $"Passed token was not valid");
        }
        
        public static LoginException TokenExpired()
        {
            return new LoginException("Token Expired", $"Passed token has expired");
        }
    }

    public partial class MethodCallHandlers : IServerCallMethods
    {
        public async Task<LoginResult> LogInWithPassword(string username, string passwordHashed)
        {
            try
            {
                (string token, UserInfo user) = await MongoConnection.Instance.LogIn(username, passwordHashed);
                
                connection.user = user;
                
                return new LoginResult
                {
                    resumeToken = token,
                    user = user
                };
            }
            catch (LoginException exception)
            {                
                return new LoginResult
                {
                    errorMessage = exception.message,
                    errorDetails = exception.details
                };
            }
        }

        public async Task<LoginResult> LogInWithToken(string token)
        {
            try
            {
                (string resumeToken, UserInfo user) = await MongoConnection.Instance.LogIn(token);
                
                connection.user = user;
                
                return new LoginResult
                {
                    resumeToken = resumeToken,
                    user        = user
                };
            }
            catch (LoginException exception)
            {
                Console.WriteLine(exception);
                
                return new LoginResult
                {
                    errorMessage = exception.message,
                    errorDetails = exception.details
                };
            }
        }

        public async Task LogOut()
        {
            connection.user = default;
        }
    }
}