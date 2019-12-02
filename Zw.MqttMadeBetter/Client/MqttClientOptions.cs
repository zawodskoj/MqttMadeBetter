using System;
using System.Net.Sockets;
using Zw.MqttMadeBetter.Channel;
using Zw.MqttMadeBetter.Channel.ControlPackets;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable MemberCanBePrivate.Global

namespace Zw.MqttMadeBetter.Client
{
    public class MqttChannelOptions
    {
        public int? SendTimeout { get; set; }
        public int? ReceiveTimeout { get; set; }
        
        public int? PingInterval { get; set; }
        public int? AcknowledgeTimeout { get; set; }
        
        public Action<Socket> CustomSocketConfig { get; set; }

        public MqttReadOnlyChannelOptions Freeze()
        {
            return new MqttReadOnlyChannelOptions(
                SendTimeout, ReceiveTimeout, PingInterval, AcknowledgeTimeout, CustomSocketConfig);
        }
    }

    public class MqttReadOnlyChannelOptions
    {
        public MqttReadOnlyChannelOptions(int? sendTimeout, int? receiveTimeout, int? pingInterval,
            int? acknowledgeTimeout, Action<Socket> customSocketConfig)
        {
            SendTimeout = sendTimeout;
            ReceiveTimeout = receiveTimeout;
            PingInterval = pingInterval;
            AcknowledgeTimeout = acknowledgeTimeout;
            CustomSocketConfig = customSocketConfig;
        }

        public int? SendTimeout { get; }
        public int? ReceiveTimeout { get; }
        
        public int? PingInterval { get; }
        public int? AcknowledgeTimeout { get; }
        
        public Action<Socket> CustomSocketConfig { get; }
    }

    public class MqttConnectionOptions
    {
        public string ClientId { get; set; }
        public MqttCredentials Credentials { get; set; }
        public MqttWillMessage WillMessage { get; set; }
        public bool CleanSession { get; set; } = true;
        public ushort KeepAliveSeconds { get; set; } = 10;

        public MqttReadOnlyConnectionOptions Freeze()
        {
            return new MqttReadOnlyConnectionOptions(
                ClientId,
                Credentials,
                WillMessage,
                CleanSession,
                KeepAliveSeconds);
        }
    }

    public class MqttReadOnlyConnectionOptions
    {
        public MqttReadOnlyConnectionOptions(string clientId, MqttCredentials credentials, MqttWillMessage willMessage, bool cleanSession, ushort keepAliveSeconds)
        {
            ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId), "Client id is not set");
            Credentials = credentials;
            WillMessage = willMessage;
            CleanSession = cleanSession;
            KeepAliveSeconds = keepAliveSeconds;
            
            if (keepAliveSeconds == 0)
                throw new ArgumentException("KeepAliveSeconds should be greater than 0", nameof(keepAliveSeconds));
        }

        public string ClientId { get; }
        public MqttCredentials Credentials { get; }
        public MqttWillMessage WillMessage { get; } 
        public bool CleanSession { get; }
        public ushort KeepAliveSeconds { get; }
    }

    public class MqttCredentials
    {
        public string Username { get; }
        public string Password { get; }

        public MqttCredentials(string username, string password = null)
        {
            Username = username ?? throw new ArgumentNullException(nameof(username));
            Password = password;
        }
    }

    public class MqttEndpoint
    {
        public MqttEndpoint(string hostname, int port)
        {
            Hostname = hostname;
            Port = port;
        }

        public string Hostname { get; }
        public int Port { get; }
    }
    
    public class MqttClientOptions
    {
        public MqttEndpoint Endpoint { get; set; }
        public MqttConnectionOptions ConnectionOptions { get; } = new MqttConnectionOptions();
        public MqttChannelOptions ChannelOptions { get; } = new MqttChannelOptions();
        public bool AutoAcknowledge { get; set; }

        public MqttReadOnlyClientOptions Freeze()
        {
            return new MqttReadOnlyClientOptions(Endpoint, ConnectionOptions.Freeze(), ChannelOptions.Freeze(), AutoAcknowledge);
        }
    }

    public class MqttReadOnlyClientOptions
    {
        public MqttReadOnlyClientOptions(MqttEndpoint endpoint, MqttReadOnlyConnectionOptions connectionOptions, MqttReadOnlyChannelOptions channelOptions, bool autoAcknowledge)
        {
            Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint), "Endpoint is not set");
            ConnectionOptions = connectionOptions;
            ChannelOptions = channelOptions;
            AutoAcknowledge = autoAcknowledge;
        }

        public MqttEndpoint Endpoint { get; }
        public MqttReadOnlyConnectionOptions ConnectionOptions { get; }
        public MqttReadOnlyChannelOptions ChannelOptions { get; }
        public bool AutoAcknowledge { get; }
    }
}