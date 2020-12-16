using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Reference: https://docs.microsoft.com/en-us/aspnet/core/fundamentals/websockets?view=aspnetcore-3.1
namespace server
{
    using Microsoft.Extensions.FileProviders;
    using OpenUp.Networking;
    using OpenUp.Networking.ServerCalls;
    using OpenUp.Updating;
    using OpenUp.Utils;
    using SessionListeners;
    using System.IO;
    using System.Linq;
    using Microsoft.Extensions.Configuration;
    using TCPTools;
    using Utils;

    public class Startup
    {
        public const string CORS_POLICY = "CORSPolicy";
        
        private List<Connection> clients = new List<Connection>();

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }
        
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            BinaryUtils.LogMethod = s => ErrorLogger.LogMessage(s, OpenUp.Networking.LogMessages.LogType.LOG, 0, "");
            BinaryUtils.LogExceptionMethod = s => ErrorLogger.LogMessage(s.ToString(), OpenUp.Networking.LogMessages.LogType.EXCEPTION, 0, "");

            try
            {
                Task.Run(
                    async () =>
                    {
                        try
                        {
                            await DBUtils.SetShapesOnStartUp();
                        }
                        catch (Exception exception)
                        {
                            Console.WriteLine("--------------- Startup Splat!! ---------------");
                            Console.WriteLine(exception);

                            ErrorLogger.StoreError(new Exception("Failed startup", exception));
                        }
                    }
                );

                UpdateApplicationTool.InitializeMap();
                OpenUp.Redux.SetupRoutine.Run();
            }
            catch (Exception exception)
            {
                ErrorLogger.StoreError(new Exception("Failed startup", exception));
            }

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();
            app.UseCors();

            try
            {
                app.UseStaticFiles(
                    new StaticFileOptions
                    {
                        FileProvider = new PhysicalFileProvider(
                            Path.Combine(Directory.GetCurrentDirectory(), "_doc_site")
                        ),
                        RequestPath = "/documentation"
                    }
                );
            }
            catch (Exception exception)
            {
                Console.WriteLine("Couldn't set up documentation site with error:");
                Console.WriteLine(exception);
            }

            app.UseEndpoints(
                endpoints =>
                {
                    endpoints.MapGet(
                        "/",
                        async context =>
                        {
                            await context.Response.WriteAsync(
                                $"Hello, World, I'm a test message"
                            );
                        }
                    );
                    endpoints.MapControllers();
                }
            );

            // Websockets
            app.UseWebSockets();

            app.Use(
                async (context, next) =>
                {
                    // Only allow websocket requests on /ws
                    if (context.Request.Path == "/ws")
                    {
                        // Parse websocket establishment request
                        if (context.WebSockets.IsWebSocketRequest)
                        {
                            // Transition request from TCP to WebSocket
                            WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                            Console.WriteLine($"Connection from {context.Connection.RemoteIpAddress} received");

                            Connection client = new Connection(webSocket)
                            {
                                SessionProvider = SessionManager.Instance,
                                endPoint = context.Connection.RemoteIpAddress,
                                loggingOptions = new ConnectionLoggingOptions
                                {
                                    types = new HashSet<BinaryMessageType> { },
                                    whiteList   = true,
                                    messageSize = true
                                }
                            };
                            client.Handler = new MethodHandler
                                             {
                                                 HandlerImplementation = new MethodCallHandlers(client)
                                             };

                            ErrorLogger.TrackErrors(client);

                            client.OnSessionJoin  += JoinLeave.OnJoin(client);
                            client.OnSessionLeave += JoinLeave.OnLeave(client);

                            clients.Add(client);
                            
                            StartSendLoop(client);

                            Console.WriteLine(
                                $"New client connected: {webSocket.GetHashCode()} ({clients.Count} online)"
                            );

                            try
                            {
                                WebSocketReceiveResult result = await client.Listen();

                                // Terminate connection using close handshake
                                Console.WriteLine("Close handshake initiated by client.");

                                if (result == null)
                                {
                                    await webSocket.CloseAsync(
                                        WebSocketCloseStatus.InternalServerError,
                                        "sorry",
                                        CancellationToken.None
                                    );
                                }
                                else
                                {
                                    await webSocket.CloseAsync(
                                        result.CloseStatus.Value,
                                        result.CloseStatusDescription,
                                        CancellationToken.None
                                    );
                                }
                            }
                            catch (WebSocketException e)
                            {
                                // Log unknown exceptions
                                if (e.WebSocketErrorCode != WebSocketError.ConnectionClosedPrematurely)
                                {
                                    Console.WriteLine(
                                        $"Unexcepted Websocket exception on socket {webSocket.GetHashCode()}"
                                    );
                                    Console.WriteLine($"{e.WebSocketErrorCode}: {e.Message}");
                                }
                                else
                                {
                                    Console.WriteLine($"Socket forcibly closed by client: {webSocket.GetHashCode()}");
                                }
                            }
                            finally
                            {
                                // Remove connection
                                clients.Remove(client);

                                if (client.session != null)
                                {
                                    client.session.RemovePlayer(SessionManager.Instance.PlayerForConnection(client));
                                    SessionManager.Instance.connections[client.session.id].Remove(client);
                                }

                                Console.WriteLine(
                                    $"Client disconnected: {webSocket.GetHashCode()}. ({clients.Count} online)"
                                );
                            }
                        }

                        // Bad request
                        // else if (context.WebSockets)
                        else
                        {
                            context.Response.StatusCode = 400;
                        }
                    }
                    else
                    {
                        await next();
                    }
                }
            );

            DBUtils.CleanUp.addRoutine(DBUtils.cleanupOutdatedRecentAssets);
            DBUtils.CleanUp.addRoutine(DBUtils.cleanupExpiredOrganisationInvites);
            DBUtils.CleanUp.addRoutine(DBUtils.cleanupOutdatedAppLogs);
            DBUtils.CleanUp.start(new TimeSpan(1, 0, 0));

            // TCP.StartListening();
        }

        private void StartSendLoop(Connection connection)
        {
            async Task Send()
            {
                while (connection.isOpen)
                {
                    await Task.Delay(16);

                    try
                    {
                        await connection.SendFrame();
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine(exception);
                    }
                }
            }

            Task.Run(Send);
        }
    }
}
