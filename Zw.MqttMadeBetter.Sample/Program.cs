using System;
using System.Threading;
using System.Threading.Tasks;
using Zw.MqttMadeBetter.ControlPackets;

namespace Zw.MqttMadeBetter.Sample
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var host = Environment.GetEnvironmentVariable("MQ_HOST");
            var port = int.Parse(Environment.GetEnvironmentVariable("MQ_PORT") ?? throw new Exception("Invalid envs"));
            var user = Environment.GetEnvironmentVariable("MQ_USER");
            var pass = Environment.GetEnvironmentVariable("MQ_PASS");

            var client = await MqttClient.Create(new MqttConnectionOptions
            {
                Hostname = host,
                Port = port,
                Username = user,
                Password = pass,
                AutoAcknowledge = true,
                CleanSession = true,
                ClientId = Guid.NewGuid().ToString("N"),
                KeepAliveSeconds = 5
            }, CancellationToken.None);

            await client.Send("mqtt/test/qos0/send", MqttMessageQos.QOS_0, ReadOnlyMemory<byte>.Empty, CancellationToken.None);
            await client.Send("mqtt/test/qos1/send", MqttMessageQos.QOS_1, ReadOnlyMemory<byte>.Empty, CancellationToken.None);
            await client.Send("mqtt/test/qos2/send", MqttMessageQos.QOS_2, ReadOnlyMemory<byte>.Empty, CancellationToken.None);
//
            client.Messages.Subscribe(x => Console.WriteLine("Received " + x));
            await client.Subscribe("mqtt/test/qos0", MqttMessageQos.QOS_0, CancellationToken.None);
            await client.Subscribe("mqtt/test/qos1", MqttMessageQos.QOS_1, CancellationToken.None);
            await client.Subscribe("mqtt/test/qos2", MqttMessageQos.QOS_2, CancellationToken.None);

            Console.WriteLine("Running");
            await Task.Run(() => Console.ReadLine());
        }

        // hello antipatterns
        private static async void LoopRecv(MqttChannel test)
        {
            while (true)
            {
                try
                {
                    Console.WriteLine("Received " + await test.Receive(CancellationToken.None));
                    LoopRecv(test);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Caught {0}: {1}", e.GetType().Name, e.Message);
                }
            }
        }
    }
}