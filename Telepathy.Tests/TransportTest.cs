﻿using NUnit.Framework;
using System;
using System.Net;
using System.Text;
using System.Threading;

namespace Telepathy.Tests
{
    [TestFixture]
    public class TransportTest
    {
        // just a random port that will hopefully not be taken
        const int port = 9587;

        Server server;

        [SetUp]
        public void Setup()
        {
            server = new Server();
            server.Start(port);

        }

        [TearDown]
        public void TearDown()
        {
            server.Stop();
        }

        [Test]
        public void DisconnectImmediateTest()
        {
            Client client = new Client();
            client.Connect("127.0.0.1", port);

            // I should be able to disconnect right away
            // if connection was pending,  it should just cancel
            client.Disconnect();

            Assert.That(client.Connected, Is.False);
        }

        [Test]
        public void SpamConnectTest()
        {
            Client client = new Client();
            for (int i = 0; i < 1000; i++)
            {
                client.Connect("127.0.0.1", port);
                Assert.That(client.Connecting || client.Connected, Is.True);
                client.Disconnect();
                Assert.That(client.Connecting, Is.False);
            }
        }

        [Test]
        public void ReconnectTest()
        {
            Client client = new Client();
            client.Connect("127.0.0.1", port);

            // wait for successful connection
            Message connectMsg = NextMessage(client);
            Assert.That(connectMsg, Is.AssignableFrom<ConnectMessage>());
            // disconnect and lets try again
            client.Disconnect();


            // connecting should flush message queue  right?
            client.Connect("127.0.0.1", port);
            // wait for successful connection
            connectMsg = NextMessage(client);
            Assert.That(connectMsg, Is.AssignableFrom<ConnectMessage>());

            client.Disconnect();
        }

        [Test]
        public void ServerTest()
        {
            Encoding utf8 = Encoding.UTF8;
            Client client = new Client();

            client.Connect("127.0.0.1", port);

            // we  should first receive a connected message
            Message connectMsg = NextMessage(server);
            Assert.That(connectMsg, Is.AssignableFrom<ConnectMessage>());


            // then we should receive the data
            client.Send(utf8.GetBytes("Hello world"));
            DataMessage dataMsg = (DataMessage)NextMessage(server);
            string str = utf8.GetString(dataMsg.data);
            Assert.That(str, Is.EqualTo("Hello world"));

            // finally when the client disconnect,  we should get a disconnected message
            client.Disconnect();
            Message disconnectMsg = NextMessage(server);
            Assert.That(disconnectMsg, Is.AssignableFrom<DisconnectMessage>());
        }

        [Test]
        public void ClientTest()
        {
            Encoding utf8 = Encoding.UTF8;
            Client client = new Client();

            client.Connect("127.0.0.1", port);

            // we  should first receive a connected message
            Message serverConnectMsg = NextMessage(server);
            int id = serverConnectMsg.connectionId;

            // we  should first receive a connected message
            Message clientConnectMsg = NextMessage(client);
            Assert.That(serverConnectMsg, Is.AssignableFrom<ConnectMessage>());

            // Send some data to the client
            server.Send(id, utf8.GetBytes("Hello world"));
            DataMessage dataMsg = (DataMessage)NextMessage(client) ;
            string str = utf8.GetString(dataMsg.data);
            Assert.That(str, Is.EqualTo("Hello world"));

            // finally if the server stops,  the clients should get a disconnect error
            server.Stop();
            Message disconnectMsg = NextMessage(client);
            Assert.That(disconnectMsg, Is.AssignableFrom<DisconnectMessage>());

            client.Disconnect();
        }

        [Test]
        public void ServerDisconnectClientTest()
        {
            Client client = new Client();

            client.Connect("127.0.0.1", port);

            // we  should first receive a connected message
            Message serverConnectMsg = NextMessage(server);
            int id = serverConnectMsg.connectionId;

            server.Disconnect(id);

        }

        [Test]
        public void ClientKickedCleanupTest()
        {
            Client client = new Client();

            client.Connect("127.0.0.1", port);

            // read connected message on client
            Message clientConnectedMsg = NextMessage(client);
            Assert.That(clientConnectedMsg, Is.AssignableFrom<ConnectMessage>());

            // read connected message on server
            Message serverConnectMsg = NextMessage(server);
            int id = serverConnectMsg.connectionId;

            // server kicks the client
            server.Disconnect(id);

            // wait for client disconnected message
            Message clientDisconnectedMsg = NextMessage(client);
            Assert.That(clientDisconnectedMsg, Is.AssignableFrom<DisconnectMessage>());

            // was everything cleaned perfectly?
            // if Connecting or Connected is still true then we wouldn't be able
            // to reconnect otherwise
            Assert.That(client.Connecting, Is.False);
            Assert.That(client.Connected, Is.False);
        }

        /*
        [Test]
        public void GetConnectionInfoTest()
        {
            // connect a client
            Client client = new Client();
            client.Connect("127.0.0.1", port);

            // get server's connect message
            Message serverConnectMsg = NextMessage(server);
            Assert.That(serverConnectMsg, Is.AssignableFrom<ConnectMessage>());


            // get server's connection info for that client
            string address;
            if (server.GetConnectionInfo(serverConnectMsg.connectionId, out address))
            {
                Assert.That(address == "127.0.0.1");
            }
            else Assert.Fail();

            client.Disconnect();
        }
*/

        [Test]
        public void LargeMessageTest()
        {
            // connect a client
            Client client = new Client();
            client.Connect("127.0.0.1", port);

            // we  should first receive a connected message
            Message serverConnectMsg = NextMessage(server);
            int id = serverConnectMsg.connectionId;

            // Send a large message,  bigger thank 64K
            client.Send(new byte[100000]);
            var dataMsg = (DataMessage)NextMessage(server);
            Assert.That(dataMsg.data.Length, Is.EqualTo(100000));

            // finally if the server stops,  the clients should get a disconnect error
            server.Stop();
            client.Disconnect();

        }

        static Message NextMessage(Server server)
        {
            Message message = server.GetNextMessage();
            int count = 0;

            while (message == null)
            {
                count++;

                if (count >= 100)
                {
                    Assert.Fail("The message did not get to the server");
                }
                Thread.Sleep(100);
                message = server.GetNextMessage();
            }

            return message;
        }

        static Message NextMessage(Client client)
        {
            Message message = client.GetNextMessage();

            int count = 0;

            while (message == null)
            {
                count++;

                if (count >= 100)
                {
                    Assert.Fail("The message did not get to the server");
                }
                Thread.Sleep(100);
                message = client.GetNextMessage();
            }

            return message;
        }

    }
}
