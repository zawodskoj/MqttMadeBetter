using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Zw.MqttMadeBetter.Channel.ControlPackets;
using Zw.MqttMadeBetter.Client;

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

            var options = new MqttClientOptions
            {
                Endpoint = new MqttEndpoint(host, port),
                ConnectionOptions =
                {
                    ClientId = Guid.NewGuid().ToString("N"),
                    Credentials = new MqttCredentials(user, pass)
                }
            }.Freeze();

            var client = await MqttClient.Create(options, CancellationToken.None);
            
            await client.Send("mqtt/test/qos0/send", MqttMessageQos.QOS_0, ReadOnlyMemory<byte>.Empty, CancellationToken.None);
            await client.Send("mqtt/test/qos1/send", MqttMessageQos.QOS_1, ReadOnlyMemory<byte>.Empty, CancellationToken.None);
            await client.Send("mqtt/test/qos2/send", MqttMessageQos.QOS_2, ReadOnlyMemory<byte>.Empty, CancellationToken.None);
//
            client.Messages.Subscribe(x => Console.WriteLine("Received " + Encoding.UTF8.GetString(x.Payload.Span)));
            await client.Subscribe("mqtt/test/qos0", MqttMessageQos.QOS_0, CancellationToken.None);
            await client.Subscribe("mqtt/test/qos1", MqttMessageQos.QOS_1, CancellationToken.None);
            await client.Subscribe("mqtt/test/qos2", MqttMessageQos.QOS_2, CancellationToken.None);

            Console.WriteLine("Running");
            // await Task.Run(() => Console.ReadLine());
            Console.ReadLine();
        }
    }
}