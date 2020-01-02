
using Fleck;
using System.Collections.Generic;
using System.Collections;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;
using System.IO;
using System;

namespace WS_Server
{
    class Server
    {
        private const int frameRate = 31;
        private const String imagesBaseURL = @"../../../images";
        private enum Images
        { 
            Earth,
            Yoda,
            BabyYoda,
        }
        public static void Main()
        {
            FleckLog.Level = LogLevel.Debug;
            var allSockets = new List<IWebSocketConnection>();
            var server = new WebSocketServer("ws://0.0.0.0:8181");
            Images selection = Images.Earth;

            List<ImageData> allImages = getSelectedImageList(selection,imagesBaseURL);

            Task sendImage = null;
            Boolean taskCompleted = false;
            Boolean selectNewImages = false;

            // Define the selectNewImages token.
            CancellationTokenSource source = new CancellationTokenSource();
            CancellationToken token = source.Token;

            Action sendDelayedIamges = () => {
                var enm = allImages.GetEnumerator();
                while (enm.MoveNext())
                {
                    if (allSockets.Count == 0) {
                        source.Cancel();
                        Console.WriteLine("Cancelling at task: no sockets left");
                        break;
                    }
                    else if(selectNewImages == true)
                    {
                        source.Cancel();
                        Console.WriteLine("Cancelling at task: selectNewImages set by user");
                        break;
                    }                  
                    allSockets.ForEach(s => s.Send(enm.Current.getImage()));
                    Thread.Sleep(frameRate);
                }
            };

            server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    Console.WriteLine("Open!");
                    allSockets.Add(socket);
                    if (sendImage == null) { 
                        sendImage = Task.Factory.StartNew(sendDelayedIamges, token, TaskCreationOptions.DenyChildAttach,
                            TaskScheduler.Default);
                        taskCompleted = sendImage.IsCompleted;
                    }
                };
                socket.OnClose = () =>
                {
                    Console.WriteLine("Close!");
                    allSockets.Remove(socket);
                };
                socket.OnMessage = message =>
                {
                    if (message.Equals("exit")) 
                    {
                        socket.Close();
                    }   
                    else if (message.Equals("earth"))
                    {
                        selection = Images.Earth;
                        selectNewImages = true;
                    }
                    else if (message.Equals("yoda"))
                    {
                        selection = Images.Yoda;
                        selectNewImages = true;
                    }
                    else if (message.Equals("babyyoda"))
                    {
                        selection = Images.BabyYoda;
                        selectNewImages = true;
                    }
                    if (selectNewImages) 
                    {
                        allImages = getSelectedImageList(selection, imagesBaseURL);
                        sendImage.Wait();
                        selectNewImages = false;
                        taskCompleted = sendImage.IsCompleted;
                        sendImage = Task.Factory.StartNew(sendDelayedIamges, CancellationToken.None, TaskCreationOptions.DenyChildAttach,
                        TaskScheduler.Default);

                    }
                    Console.WriteLine(message);                 
                };
            });

            while (true)
            {
                if (allSockets.Count > 0 && taskCompleted) {
                    sendImage = Task.Factory.StartNew(sendDelayedIamges, CancellationToken.None, TaskCreationOptions.DenyChildAttach,
                        TaskScheduler.Default);
                }
                if (sendImage != null) { 
                    taskCompleted = sendImage.IsCompleted;
                }
                //input = Console.ReadLine();
            }
        }
        private static List<ImageData> getSelectedImageList(Images selection, String imagesBaseURL) 
        {
            List<ImageData> list = new List<ImageData>();

            switch (selection) 
            {
                case Images.Earth:
                    imagesBaseURL+="/earth";
                    break;
                case Images.Yoda:
                    imagesBaseURL += "/yoda";
                    break;
                case Images.BabyYoda:
                    imagesBaseURL += "/babyyoda";
                    break;

            }
            Directory.GetFiles(imagesBaseURL).ToList().ForEach(file => list.Add(new ImageData(Path.GetFileName(file), file)));
            return list;
        }
    }
}
