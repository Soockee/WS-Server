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
            // ServiceCollections holds the ILogger and Loggingfactory used by Jaeger
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var logger = serviceProvider.GetService<ILogger<Server>>();
            var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            // jaeger tracer: Service name is used to identifiy the trace in the UI, loggerFactory as loggingframework
            var tracer = InitTracer("WS_Server", loggerFactory);

            Server server = new Server("ws://0.0.0.0:8181", ImageSelection.Earth, @"../../../images", 31, serviceProvider.GetService<ILogger<Server>>(), tracer);


            /**
             * Serverlogic:
             *      ->OnOpen:
             *          Adds opened socket to socketlist
             *          Starts the task, which sis responsible for sending frames to the client
             *          updates taskstatus
            */
            
            Run(server, tracer);
            tracer.Dispose();
        }

        private static void Run(Server server, Tracer tracer) 
        {
            using (var Mainscope = tracer.BuildSpan("RunServer").StartActive(true))
            {
                server.WebSocketServer.Start(socket =>
                {
                    socket.OnOpen = () =>
                    {
                        using (var scope = tracer.BuildSpan("Open-Connection").StartActive(true))
                        {
                            Console.WriteLine("Open!");
                            server.AllSockets.Add(socket);
                        /*if (server.SendImage == null)
                        {
                            server.SendImage = Task.Factory.StartNew(server.createSendDelayedFramesAction(), server.Token, TaskCreationOptions.DenyChildAttach,
                                TaskScheduler.Default);
                            server.IsTaskCompleted = server.SendImage.IsCompleted;
                        }*/
                        }
                    };
                    socket.OnClose = () =>
                    {
                        using (var scope = tracer.BuildSpan("Close-Connection").StartActive(true))
                        {
                            Console.WriteLine("Close!");
                            server.AllSockets.Remove(socket);
                        }
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
                    if (server.AllSockets.Count == 0 && server.IsTaskCompleted)
                    {
                        break;
                    }
                }
            }
        }
        /**
         * Configures the LoggingService. AddLogging is creating a ILogger and LoggerFactory Service
         * which is used by the LoggingFramework and therefore Jaeger
         */
        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(configure => configure.AddConsole());

        }

        /**
         * Builds and returns a tracer with default configuration
         * Source for InitTracer():  
         * https://github.com/yurishkuro/opentracing-tutorial/tree/master/csharp/src/lesson01 
         */
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
