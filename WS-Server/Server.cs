
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
        private const int frameRate = 300;
        public static void Main()
        {
            FleckLog.Level = LogLevel.Debug;
            var allSockets = new List<IWebSocketConnection>();
            var server = new WebSocketServer("ws://0.0.0.0:8181");
            var allImages = new List<ImageData>();
            Directory.GetFiles(@"../../../images").ToList().ForEach(file => allImages.Add(new ImageData(Path.GetFileName(file), file)));

            Action sendDelayedIamges = () => {
                var enm = allImages.GetEnumerator();
                while (enm.MoveNext())
                {
                    allSockets.ForEach(s => s.Send(enm.Current.getImage()));
                    Thread.Sleep(frameRate);
                }
            };

            Task sendImage = null;
            Boolean taskCompleted = false;


            server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    Console.WriteLine("Open!");
                    allSockets.Add(socket);
                    sendImage = Task.Factory.StartNew(sendDelayedIamges, CancellationToken.None, TaskCreationOptions.DenyChildAttach,
                        TaskScheduler.Default);
                    taskCompleted = sendImage.IsCompleted;
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
                    Console.WriteLine(message);

                    //while (message != "exit") {
                    // }
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
      
    }
}
