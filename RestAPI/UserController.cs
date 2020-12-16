using Microsoft.AspNetCore.Mvc;

namespace server.RestAPI
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading.Tasks;
    using BCrypt.Net;
    using DBConnection;
    using Microsoft.AspNetCore.Cors;
    using OpenUp.Networking.ServerCalls;

    internal class InvalidRegistrationException : Exception
    {
        public InvalidRegistrationException(string reason) : base(reason) {} 
    }

    [ApiController]
    public class UserController : ControllerBase
    {
        public class NewUserData
        {
            public string username { get; set; }
            public string passSHA256 { get; set; }
            public string useremail { get; set; }

            public async Task Validate()
            {
                if (String.IsNullOrWhiteSpace(username))
                {
                    throw new InvalidRegistrationException("Cannot add user without valid username");
                }
                
                User user = await MongoConnection.Instance.GetUser(username);

                if (user != null)
                {
                    throw new InvalidRegistrationException("User with that name already exists");
                }

                if (passSHA256 == null)
                {
                    throw new InvalidRegistrationException("Must have a password");
                }
                if (passSHA256.Length != 64)
                {
                    throw new InvalidRegistrationException("Password must be hashed using the SHA256 algorithm");
                }
            }

            public override string ToString()
            {
                return $"username: {username}\n" +
                       $"passSHA256: {passSHA256}\n" +
                       $"useremail: {useremail}";
            }
        }

        public class LoginData
        {
            public string passSHA256 { get; set; }
            public string username { get; set; }
        }

        public object key_loggedIn = new object();
        public const string AUTH_KEY_NAME = "OpenUp-Auth-Key";
        
        [Route("api/users/{userID}")]
        [EnableCors(Startup.CORS_POLICY)]
        [HttpGet]
        public async Task<User> Get(string userID)
        {
            User user = await MongoConnection.Instance.GetUser(userID);

            if (user == null)
            {
                throw new NotSupportedException("User not found");
            }

            return user;
        }
        
        [Route("api/testing/{name}")]
        [EnableCors(Startup.CORS_POLICY)]
        [HttpGet]
        public async Task<string> GetHelloWorld(string name)
        {
            await Task.Delay(500);

            return $"Hello, {name}";
        }

        [Route("api/users/login")]
        [EnableCors(Startup.CORS_POLICY)]
        [HttpPost]
        public async Task<LoginResult> LogIn(LoginData loginData)
        {
            Console.WriteLine($"Attempting to log in with data: {loginData.username}, {loginData.passSHA256}");

            try
            {
                (string token, UserInfo user) = await MongoConnection.Instance.LogIn(loginData.username, loginData.passSHA256);
            
                return new LoginResult
                       {
                           resumeToken = token,
                           user        = user
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
            catch (Exception exception)
            {
                return new LoginResult
                       {
                           errorMessage = exception.Message,
                       };
            }
            
        }
        
        [Route("api/users")]
        [EnableCors(Startup.CORS_POLICY)]
        [HttpPost]
        public async Task<string> AddUser(NewUserData user)
        {
            Console.WriteLine($"Adding a user {user}");

            try
            {
                await user.Validate();

                User newUser = new User
                               {
                                   username = user.username,
                                   services = new User.Services
                                              {
                                                  password = new User.Password
                                                             {
                                                                 bcrypt = BCrypt.HashPassword(user.passSHA256)
                                                             }
                                              }
                               };

                await MongoConnection.Instance.users.InsertOneAsync(newUser);

                newUser = await MongoConnection.Instance.GetUser(user.useremail);

                UserInfo userInfo = new UserInfo
                                    {
                                        id = newUser.id,
                                        name = newUser.username
                                    };

                return JsonSerializer.Serialize(userInfo);
            }
            catch (InvalidRegistrationException exception)
            {
                return JsonSerializer.Serialize(exception);
            }
            catch (Exception exception)
            {
                return JsonSerializer.Serialize(new Exception("Internal server error"));
            }
        }    
    }
}