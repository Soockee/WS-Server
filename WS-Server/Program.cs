using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;


using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.DependencyInjection;

using Jaeger;
using Jaeger.Samplers;

namespace WS_Server
{
    class Program
    {
        public static void Main(string[] args)
        {
            //FleckLog.Level = LogLevel.Debug;
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var logger = serviceProvider.GetService<ILogger<Server>>();
            var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            var tracer = InitTracer("WS_Server", loggerFactory);

            Server server = new Server("ws://0.0.0.0:8181", ImageSelection.Earth, @"../../../images", 64, serviceProvider.GetService<ILogger<Server>>(), tracer);

            server.WebSocketServer.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    Console.WriteLine("Open!");
                    server.AllSockets.Add(socket);
                    if (server.SendImage == null)
                    {
                        server.SendImage = Task.Factory.StartNew(server.createSendDelayedFramesAction(), server.Token, TaskCreationOptions.DenyChildAttach,
                            TaskScheduler.Default);
                        server.IsTaskCompleted = server.SendImage.IsCompleted;
                    }
                };
                socket.OnClose = () =>
                {
                    Console.WriteLine("Close!");
                    server.AllSockets.Remove(socket);
                };
                socket.OnMessage = message =>
                {
                    server.handleOnMassage(socket, message);
                };
            });

            while (true)
            {
                if (server.AllSockets.Count > 0 && server.IsTaskCompleted)
                {
                    server.SendImage = Task.Factory.StartNew(server.createSendDelayedFramesAction(), CancellationToken.None, TaskCreationOptions.DenyChildAttach,
                        TaskScheduler.Default);
                }
                if (server.SendImage != null)
                {
                    server.setTaskCompleted(server.SendImage.IsCompleted);
                }
                //input = Console.ReadLine();
            }
        }
        
        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(configure => configure.AddConsole());

        }
        private static Tracer InitTracer(string serviceName, ILoggerFactory loggerFactory)
        {
            var samplerConfiguration = new Configuration.SamplerConfiguration(loggerFactory)
                .WithType(ConstSampler.Type)
                .WithParam(1);

            var reporterConfiguration = new Configuration.ReporterConfiguration(loggerFactory)
                .WithLogSpans(true);

            return (Tracer)new Configuration(serviceName, loggerFactory)
                .WithSampler(samplerConfiguration)
                .WithReporter(reporterConfiguration)
                .GetTracer();
        }
    }
}
