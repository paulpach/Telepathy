using System;
using System.Diagnostics;
using System.Threading;
using Telepathy;

namespace Telepathy.LoadTest
{
    public class RunServer
    {
        public static void StartServer(int port)
        {
            // start server
            Server server = new Server();
            server.Start(port);
            int serverFrequency = 60;
            Logger.Log("started server");

            long messagesReceived = 0;
            long dataReceived = 0;
            Stopwatch stopwatch = Stopwatch.StartNew();

            while (true)
            {
                for (Message msg = server.GetNextMessage(); msg != null;  msg = server.GetNextMessage())
                {
                    if (msg is DataMessage dataMessage)
                    {
                        server.Send(msg.connectionId, dataMessage.data);

                        messagesReceived++;
                        dataReceived += dataMessage.data.Length;
                    }
                }

                // sleep
                Thread.Sleep(1000 / serverFrequency);

                // report every 10 seconds
                if (stopwatch.ElapsedMilliseconds > 1000 * 10)
                {
                    Logger.Log(string.Format("Server in={0} ({1} KB/s)  out={0} ({1} KB/s)", messagesReceived, (dataReceived * 1000 / (stopwatch.ElapsedMilliseconds * 1024))));
                    stopwatch.Stop();
                    stopwatch = Stopwatch.StartNew();
                    messagesReceived = 0;
                    dataReceived = 0;
                }

            }
        }


    }
}
