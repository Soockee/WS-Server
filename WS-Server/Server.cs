﻿using System.Collections.Generic;
using System.Collections;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;
using System.IO;
using System;

using Fleck;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.DependencyInjection;

using OpenTracing.Util;
using Jaeger;
using Jaeger.Samplers;




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
        private readonly ILogger<Server> _logger;
        private readonly OpenTracing.ITracer _tracer;

        private int frameRate;
        private String imagesBaseURL = @"../../../images";
        private WebSocketServer webSocketServer;
        private List<IWebSocketConnection> allSockets;
        private String address;
        private ImageSelection selection;
        private List<ImageData> allImages;
        private Task sendImage = null;
        private Boolean taskCompleted = false;
        private Boolean selectNewImages = false;
        private CancellationTokenSource source;
        private CancellationToken token;
        public Server(String address, ImageSelection selection, String imagesBaseURL,
            int frameRate, ILogger<Server> logger, OpenTracing.ITracer tracer) 
        { 
            this.frameRate = frameRate;
            this.imagesBaseURL = imagesBaseURL;
            this.address = address;
            this.webSocketServer = new WebSocketServer(this.address);
            this.allSockets = new List<IWebSocketConnection>();
            this.selection = selection;
            this.allImages = getSelectedImageList(selection, imagesBaseURL);
            this.source = new CancellationTokenSource();
            this.token = source.Token;
            this._logger = logger;
            this._tracer = tracer;
        }
        /*
         * handleOnMassage() handles userinput and acts accordingly
         */
        public void handleOnMassage(IWebSocketConnection socket,String message) 
        {
            using (var scope = _tracer.BuildSpan("handleOnMassage").StartActive(true))
            {
                _logger.LogInformation("Function: handleOnMassage(" + message+")");
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
                    if (sendImage != null) 
                    { 
                        this.sendImage.Wait();
                        this.taskCompleted = this.sendImage.IsCompleted;
                    }
                    this.selectNewImages = false;
                    this.sendImage = Task.Factory.StartNew(this.createSendDelayedFramesAction(), CancellationToken.None, TaskCreationOptions.DenyChildAttach,
                    TaskScheduler.Default);

                }
                scope.Span.Log(new Dictionary<string, object>
                {
                    [OpenTracing.LogFields.Event] = "ImageSelection",
                    ["value"] = selection
                });
                Console.WriteLine(message);
            }
        }

        /*
         * createSendDelayedFramesAction() returns an actions used by the task which iterates ONCE through the list of ImageData
         * and sends the image in binaryformat over the wire 
         * sendDelayedIamges is an asynchrone Task which creates its own Thread
         */
        public Action createSendDelayedFramesAction() 
        {
            Action sendDelayedImages = () => {
                var enm = this.allImages.GetEnumerator();
                using (var scope = _tracer.BuildSpan("start-animation-to-socket").StartActive(true))
                {
                    scope.Span.SetTag("AnimationType", selection.ToString());
                    _logger.LogInformation(Thread.GetCurrentProcessorId().ToString());
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
                        this.allSockets.ForEach(s => sendFrame(s, enm.Current));
                        Thread.Sleep(this.frameRate);
                    }
                }
            };
            return sendDelayedImages;
        }

        private void sendFrame(IWebSocketConnection ws , ImageData imagedata) 
        {
            using (var scope = _tracer.BuildSpan("send-frame").StartActive(true)) {
                scope.Span.Log(new Dictionary<string, object>
                {
                    [OpenTracing.LogFields.Event] = "SendFrame",
                    ["frame-name"] = imagedata.getFileName()
                });
                ws.Send(imagedata.getImage());
            }
        }  

        /*
         * getSelectedImageList() Returns List of Imagedata based on Selection. imagesBaseURL is used as searchpath. imagesBaseURL is Runtimedependent
         */
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
        public Boolean isTaskCompleted() 
        { return taskCompleted; }
        public void setTaskCompleted(Boolean taskCompleted)
        { this.taskCompleted = taskCompleted; }

        /*
         *Getter And Setter Methods
         */
        public Boolean IsTaskCompleted{  get { return taskCompleted; } set { taskCompleted = value; } }
        public WebSocketServer WebSocketServer { get { return webSocketServer; } }
        public List<IWebSocketConnection> AllSockets { get { return allSockets; } }
        public Task SendImage { get { return sendImage; } set { sendImage = value; } }
        public CancellationToken Token { get { return token; } set { token = value; } }
    }
}
