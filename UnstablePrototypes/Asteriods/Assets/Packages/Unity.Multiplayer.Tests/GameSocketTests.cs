using System;
using System.Net;
using NUnit.Framework;

using UnityEngine;

using Unity.Collections;
using Unity.Multiplayer;

namespace Unity.Multiplayer.Tests
{
    public class GameSocketTests
    {
        SocketConfiguration m_ServerConfiguration;
        SocketConfiguration m_ClientConfiguration;
        GameSocket m_Server;
        GameSocket m_Client;
        const int k_ServerPort = 4096;
        const int k_ClientPort = 4097;

        [SetUp]
        public void Initialize()
        {
            m_ClientConfiguration = new SocketConfiguration()
            {
                SendBufferSize = ushort.MaxValue,
                RecvBufferSize = ushort.MaxValue,
                Timeout = uint.MaxValue,
                MaximumConnections = 1
            };
            m_ServerConfiguration = m_ClientConfiguration;
            m_ServerConfiguration.MaximumConnections = 10;

            m_Server = new GameSocket("0.0.0.0", k_ServerPort, m_ServerConfiguration);
            m_Client = new GameSocket("127.0.0.1", k_ClientPort, m_ClientConfiguration);

            m_Server.ListenForConnections();
        }

        [TearDown]
        public void Terminate()
        {
            m_Client.Dispose();
            m_Server.Dispose();
        }

        [Test]
        public void CreateSocket_UseAlreadyUsedPort_ShouldThrowError()
        {
            bool didFail = false;
            try
            {
                using (var socket = new GameSocket("127.0.0.1", k_ClientPort, m_ClientConfiguration))
                {
                    socket.Close();
                }
            }
            catch (GameSocketException e)
            {
                Assert.AreEqual(e.Message, "Create GameSocket failed");
                didFail = true;
            }
            Assert.IsTrue(didFail);
        }

        [Test]
        public void CreateGameSockets_BindAnyAndConnect_ShouldGetConnections()
        {
            m_Client.Connect("127.0.0.1", k_ServerPort);

            ConnectTogether(m_Server, m_Client, 1000);
        }

        [Test]
        public void CreateGameSockets_SendMessage_ShouldGetMessage()
        {
            int serverId = m_Client.Connect("127.0.0.1", k_ServerPort);

            int serverConnection, clientConnection;
            ulong serverSequence, clientSequence;
            ConnectTogether(m_Server, m_Client, 5000, out serverConnection, out serverSequence, out clientConnection, out clientSequence);

            var clientsConnected = new NativeArray<int>(10, Allocator.Temp);
            clientsConnected[0] = serverConnection;

            var message = new NativeArray<byte>(7, Allocator.Temp);
            message[0] = (byte)'m';
            message[1] = (byte)'e';
            message[2] = (byte)'s';
            message[3] = (byte)'s';
            message[4] = (byte)'a';
            message[5] = (byte)'g';
            message[6] = (byte)'e';

            m_Client.SendData(new NativeSlice<byte>(message), new NativeSlice<int>(clientsConnected, 0, 1));

            int data = 0, iterations = 0;
            NativeSlice<byte> outBuffer = default(NativeSlice<byte>);
            while (data == 0)
            {
                Assert.Less(iterations++, 5000);

                m_Client.ReceiveEvent(out outBuffer, out serverConnection);
                if (m_Server.ReceiveEvent(out outBuffer, out clientConnection) == GameSocketEventType.Data)
                    data++;
            }

            Assert.AreEqual(outBuffer.Length, message.Length);
            for (int i = 0; i < message.Length; i++)
            {
                Assert.AreEqual(message[i], outBuffer[i]);
            }

            message.Dispose();
            clientsConnected.Dispose();
        }

        [Test]
        public void CreateGameSockets_BindAnyAndConnectMultiple_ShouldGetConnections()
        {
            int serverId;
            var client0 = new GameSocket("127.0.0.1", 4098, m_ClientConfiguration);
            var client1 = new GameSocket("127.0.0.1", 4099, m_ClientConfiguration);
            var client2 = new GameSocket("127.0.0.1", 4100, m_ClientConfiguration);
            var client3 = new GameSocket("127.0.0.1", 4101, m_ClientConfiguration);
            var client4 = new GameSocket("127.0.0.1", 4102, m_ClientConfiguration);

            serverId = client0.Connect("127.0.0.1", k_ServerPort);
            serverId = client1.Connect("127.0.0.1", k_ServerPort);
            serverId = client2.Connect("127.0.0.1", k_ServerPort);
            serverId = client3.Connect("127.0.0.1", k_ServerPort);
            serverId = client4.Connect("127.0.0.1", k_ServerPort);

            int clients = 0;
            int servers = 0;
            int iterations = 0;

            int serverConnection;

            int clientConnection;

            while (clients != 5 || servers != 5)
            {
                Assert.Less(iterations++, 1000);

                NativeSlice<byte> outBuffer;
                if (client0.ReceiveEvent(out outBuffer, out serverConnection) == GameSocketEventType.Connect)
                    servers++;

                if (client1.ReceiveEvent(out outBuffer, out serverConnection) == GameSocketEventType.Connect)
                    servers++;

                if (client2.ReceiveEvent(out outBuffer, out serverConnection) == GameSocketEventType.Connect)
                    servers++;

                if (client3.ReceiveEvent(out outBuffer, out serverConnection) == GameSocketEventType.Connect)
                    servers++;

                if (client4.ReceiveEvent(out outBuffer, out serverConnection) == GameSocketEventType.Connect)
                    servers++;

                if (m_Server.ReceiveEvent(out outBuffer, out clientConnection) == GameSocketEventType.Connect)
                    clients++;
            }
            Debug.Assert(clients == 5);
            Debug.Assert(servers == 5);

            client0.Dispose();
            client1.Dispose();
            client2.Dispose();
            client3.Dispose();
            client4.Dispose();
        }

        [Test]
        public void CreateGameSockets_ListenAndConnectWithIPv6()
        {
            /*
            var localServer = new GameSocket(IPAddress.IPv6Loopback.ToString(), 4098, m_ServerConfiguration);
            var localClient = new GameSocket(IPAddress.IPv6Loopback.ToString(), 4099, m_ClientConfiguration);

            localClient.Connect(IPAddress.IPv6Loopback.ToString(), 4098);

            ConnectTogether(localServer, localClient, 1000);

            localClient.Dispose();
            localServer.Dispose();
            */
        }

        [Test]
        public void UseEmptySocketConstructor()
        {
            //GameSocket socket = new GameSocket();
            //socket.Connect("127.0.0.1", k_ServerPort);
        }

        [Test]
        public void ClientServer_DisconnectReconnectMultiple()
        {
            /*int connectionCount = 10;

            var clients = new GameSocket[connectionCount];
            for (int i = 0; i < connectionCount; i++)
            {
                clients[i] = new GameSocket("127.0.0.1", (ushort)(k_ClientPort + 1 + i), m_Configuration);
                clients[i].Connect("127.0.0.1", k_ServerPort);
                ConnectTogether(m_Server, clients[i], 1000);
            }
            for (int i = 0; i < connectionCount; i++)
            {
                clients[i].Disconnect(0);
            }
            for (int i = 0; i < connectionCount; i++)
            {
                clients[i].Connect("127.0.0.1", k_ServerPort);
                ConnectTogether(m_Server, clients[i], 1000);
            }
            for (int i = 0; i < connectionCount; i++)
            {
                clients[i].Dispose();
            }*/
        }

        [Test]
        public void ClientServer_UseHelpers_EchoPingPong()
        {
            var serverConfig = new SocketConfiguration()
            {
                RecvBufferSize = 0x7fff,
                SendBufferSize = 0x7fff,
                Timeout = int.MaxValue,
                MaximumConnections = 10
            };
            var clientConfig = serverConfig;
            clientConfig.MaximumConnections = 1;

            ushort serverPort = 16384;
            ushort clientPort = 16385;
            int maxIterations = 1000;
            int messages = 10;
            int clients = 1;

            var server = new Server(serverPort, serverConfig, messages * 2, clients, maxIterations);
            var client = new Client(clientPort, serverPort, clientConfig, messages, maxIterations);

            bool done = false;
            while (!done)
            {
                done = server.Update();
                done &= client.Update();
            }

            server.Dispose();
            client.Dispose();

            Assert.Less(client.iterationCounter, maxIterations);
            Assert.Less(server.iterationCounter, maxIterations);
        }

        [Test]
        public void ClientServer_UseHelpersMultipleClients_EchoPingPong()
        {
            var serverConfig = new SocketConfiguration()
            {
                RecvBufferSize = 0x7fff,
                SendBufferSize = 0x7fff,
                Timeout = int.MaxValue,
                MaximumConnections = 16
            };
            var clientConfig = serverConfig;
            clientConfig.MaximumConnections = 1;

            ushort serverPort = 16384;
            ushort clientPort = 16385;
            int maxIterations = 5000;
            int messages = 100;
            int clients = 8;

            var server = new Server(serverPort, serverConfig, messages * 2, clients, maxIterations);

            var clientList = new System.Collections.Generic.List<Client>();
            for (int i = 0; i < clients; ++i)
            {
                clientList.Add(new Client(clientPort++, serverPort, clientConfig, messages, maxIterations));
            }

            bool done = false;
            while (!done)
            {
                done = server.Update();
                foreach (var client in clientList)
                    done &= client.Update();
            }

            server.Dispose();
            foreach (var client in clientList)
                client.Dispose();

            foreach (var client in clientList)
                Assert.Less(client.iterationCounter, maxIterations);

            Assert.Less(server.iterationCounter, maxIterations);
        }

        private void ConnectTogether(GameSocket server, GameSocket client, int maxIterations)
        {
            int serverConnection, clientConnection;
            ulong serverSequence, clientSequence;
            ConnectTogether(server, client, maxIterations, out serverConnection, out serverSequence, out clientConnection, out clientSequence);
        }

        private void ConnectTogether(GameSocket server, GameSocket client, int maxIterations, out int serverConnection, out ulong serverSequence, out int clientConnection, out ulong clientSequence)
        {
            int clients = 0, servers = 0, iterations = 0;
            serverConnection = clientConnection = 0;
            serverSequence = clientSequence = 0;

            while (clients != 1 || servers != 1)
            {
                Assert.Less(iterations++, maxIterations);

                NativeSlice<byte> outBuffer;
                if (client.ReceiveEvent(out outBuffer, out serverConnection) == GameSocketEventType.Connect)
                    servers++;

                if (server.ReceiveEvent(out outBuffer, out clientConnection) == GameSocketEventType.Connect)
                    clients++;
            }
        }
    }

    ////////////////////////////////////////////////////////////////////////////////

    public class Server
    {
        enum ServerState
        {
            Created,
            Listening,
            Done,
            Error
        }

        int maxIterations;
        int expectedClientCount;
        int expectedMessageCount;
        public int iterationCounter;
        GameSocket socket;
        ServerState state;
        NativeArray<int> connections;
        int connectionCounter;
        int messageCounter;

        public bool Done { get; internal set; }
        public Server(ushort port, SocketConfiguration config, int messages, int clients, int iterations)
        {
            messageCounter = 0;
            connectionCounter = 0;
            iterationCounter = 0;
            expectedMessageCount = messages;
            expectedClientCount = clients;
            maxIterations = iterations;

            socket = new GameSocket("127.0.0.1", port, config);
            socket.ListenForConnections();

            Done = false;
            connections = new NativeArray<int>(10, Allocator.Persistent);

            state = ServerState.Listening;
        }

        public void Dispose()
        {
            socket.Dispose();
            connections.Dispose();
        }

        public bool Update()
        {
            int id;
            NativeSlice<byte> outBuffer;
            GameSocketEventType type =
                socket.ReceiveEvent(out outBuffer, out id);

            switch (type)
            {
                case GameSocketEventType.Connect:
                    connectionCounter++;
                    break;
                case GameSocketEventType.Data:
                    Assert.AreEqual(outBuffer.Length, 4);
                    messageCounter++;
                    connections[0] = id;
                    socket.SendData(outBuffer, new NativeSlice<int>(connections, 0, 1));
                    break;
                case GameSocketEventType.Disconnect:
                default:
                    break;
            }
            if (connectionCounter == expectedClientCount &&
                (expectedMessageCount * expectedClientCount) == messageCounter)
                return true;
            return (iterationCounter++ > maxIterations);
        }
    }

    public class Client
    {
        enum ClientState
        {
            Created,
            Connecting,
            Connected,
            Ping,
            Pong,
            Done,
            Error
        }

        int maxIterations;
        public int iterationCounter;
        int messageCount;
        int messagesToSend;
        int connection;
        ushort serverPort;
        GameSocket socket;
        ClientState state;
        NativeArray<byte> ping;
        NativeArray<byte> pong;
        NativeArray<int> connections;

        public Client(ushort port, ushort serverPort, SocketConfiguration config, int messages, int maxIterations)
        {
            iterationCounter = 0;
            messageCount = 0;
            messagesToSend = messages;
            this.serverPort = serverPort;
            this.maxIterations = maxIterations;
            connection = -1;
            socket = new GameSocket("127.0.0.1", port, config);
            ping = new NativeArray<byte>(4, Allocator.Persistent);
            pong = new NativeArray<byte>(4, Allocator.Persistent);
            ping[0] = (byte)'p';
            ping[1] = (byte)'i';
            ping[2] = (byte)'n';
            ping[3] = (byte)'g';

            pong[0] = (byte)'p';
            pong[1] = (byte)'o';
            pong[2] = (byte)'n';
            pong[3] = (byte)'g';

            connections = new NativeArray<int>(10, Allocator.Persistent);

            state = ClientState.Created;
        }

        public void Dispose()
        {
            socket.Dispose();
            ping.Dispose();
            pong.Dispose();
            connections.Dispose();
        }

        public bool Update()
        {
            int id;
            NativeSlice<byte> outBuffer;
            GameSocketEventType type =
                socket.ReceiveEvent(out outBuffer, out id);

            switch (state)
            {
                case ClientState.Created:
                    connection = socket.Connect("127.0.0.1", serverPort);
                    state = ClientState.Connecting;
                    break;
                case ClientState.Connecting:
                    if (type == GameSocketEventType.Connect && id == connection)
                    {
                        connections[0] = id;
                        state = ClientState.Connected;
                    }
                    break;
                case ClientState.Connected:
                    socket.SendData(new NativeSlice<byte>(ping), new NativeSlice<int>(connections, 0, 1));
                    state = ClientState.Ping;

                    break;
                case ClientState.Ping:
                    if (type == GameSocketEventType.Data && id == connection)
                    {
                        Assert.AreEqual(outBuffer.Length, 4);
                        for (int i = 0; i < 4; ++i)
                        {
                            Assert.AreEqual(outBuffer[i], ping[i]);
                        }

                        socket.SendData(new NativeSlice<byte>(pong), new NativeSlice<int>(connections, 0, 1));
                        state = ClientState.Pong;
                    }
                    break;
                case ClientState.Pong:
                    if (type == GameSocketEventType.Data && id == connection)
                    {
                        Assert.AreEqual(outBuffer.Length, 4);
                        for (int i = 0; i < 4; ++i)
                        {
                            Assert.AreEqual(outBuffer[i], pong[i]);
                        }

                        if (++messageCount < messagesToSend)
                            state = ClientState.Connected;
                        else
                            state = ClientState.Done;
                    }
                    break;
                case ClientState.Done:
                    return true;
                case ClientState.Error:
                    break;
                default:
                    break;
            }
            return (iterationCounter++ > maxIterations);
        }
    }
}