
using Fleck;
using Microsoft.Extensions.Logging;

using System.Collections.Generic;
using System.Collections;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;
using System.IO;
using System;


namespace WS_Server
{
    public enum ImageSelection
    { 
        Earth,
        Yoda,
        BabyYoda,
    }
    class Server
    {
        private int frameRate;
        private String imagesBaseURL = @"../../../images";
        private ILogger<Server> _logger;
        private WebSocketServer ws;
        private List<IWebSocketConnection> allSockets;
        private ImageSelection selection;
        private List<ImageData> allImages;
        private Task sendImage = null;
        private Boolean taskCompleted = false;
        private Boolean selectNewImages = false;
        private CancellationTokenSource source;
        private CancellationToken token;

        public Server(String address, ImageSelection selection, String imagesBaseURL, int frameRate ) 
        {
            this.frameRate = frameRate;
            this.imagesBaseURL = imagesBaseURL;
            this.ws = new WebSocketServer(address);
            this.allSockets = new List<IWebSocketConnection>();
            this.selection = selection;
            this.allImages = getSelectedImageList(selection, imagesBaseURL);
            this.source = new CancellationTokenSource();
            this.token = source.Token;
        }
        public static void Main()
        {
            Server server = new Server("ws://0.0.0.0:8181", ImageSelection.Earth, @"../../../images",64);
            //FleckLog.Level = LogLevel.Debug;


            server.ws.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    Console.WriteLine("Open!");
                    server.allSockets.Add(socket);
                    if (server.sendImage == null) {
                        server.sendImage = Task.Factory.StartNew(server.createSendDelayedFramesAction(), server.token, TaskCreationOptions.DenyChildAttach,
                            TaskScheduler.Default);
                        server.taskCompleted = server.sendImage.IsCompleted;
                    }
                };
                socket.OnClose = () =>
                {
                    Console.WriteLine("Close!");
                    server.allSockets.Remove(socket);
                };
                socket.OnMessage = message =>
                {
                    server.handleOnMassage(socket, message);
                };
            });

            while (true)
            {
                if (server.allSockets.Count > 0 && server.taskCompleted) {
                    server.sendImage = Task.Factory.StartNew(server.createSendDelayedFramesAction(), CancellationToken.None, TaskCreationOptions.DenyChildAttach,
                        TaskScheduler.Default);
                }
                if (server.sendImage != null) {
                    server.taskCompleted = server.sendImage.IsCompleted;
                }
                //input = Console.ReadLine();
            }
        }
        private void initTracer() 
        {
        }


        private void handleOnMassage(IWebSocketConnection socket,String message) 
        {
            if (message.Equals("exit"))
            {
                socket.Close();
            }
            else if (message.Equals("earth"))
            {
                this.selection = ImageSelection.Earth;
                this.selectNewImages = true;
            }
            else if (message.Equals("yoda"))
            {
                this.selection = ImageSelection.Yoda;
                this.selectNewImages = true;
            }
            else if (message.Equals("babyyoda"))
            {
                this.selection = ImageSelection.BabyYoda;
                this.selectNewImages = true;
            }
            if (this.selectNewImages)
            {
                this.allImages = getSelectedImageList(this.selection, this.imagesBaseURL);
                this.sendImage.Wait();
                this.selectNewImages = false;
                this.taskCompleted = this.sendImage.IsCompleted;
                this.sendImage = Task.Factory.StartNew(this.createSendDelayedFramesAction(), CancellationToken.None, TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default);

            }
            Console.WriteLine(message);
        }
        private Action createSendDelayedFramesAction() 
        {
            Action sendDelayedIamges = () => {
                var enm = this.allImages.GetEnumerator();
                while (enm.MoveNext())
                {
                    if (this.allSockets.Count == 0)
                    {
                        this.source.Cancel();
                        Console.WriteLine("Cancelling at task: no sockets left");
                        break;
                    }
                    else if (this.selectNewImages == true)
                    {
                        this.source.Cancel();
                        Console.WriteLine("Cancelling at task: selectNewImages set by user");
                        break;
                    }
                    this.allSockets.ForEach(s => s.Send(enm.Current.getImage()));
                    Thread.Sleep(this.frameRate);
                }
            };
            return sendDelayedIamges;
        }
        private static List<ImageData> getSelectedImageList(ImageSelection selection, String imagesBaseURL) 
        {
            List<ImageData> list = new List<ImageData>();

            switch (selection) 
            {
                case ImageSelection.Earth:
                    imagesBaseURL+="/earth";
                    break;
                case ImageSelection.Yoda:
                    imagesBaseURL += "/yoda";
                    break;
                case ImageSelection.BabyYoda:
                    imagesBaseURL += "/babyyoda";
                    break;

            }
            Directory.GetFiles(imagesBaseURL).ToList().ForEach(file => list.Add(new ImageData(Path.GetFileName(file), file)));
            return list;
        }

    }
}
