using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Zw.MqttMadeBetter.Channel.ControlPackets;
using Zw.MqttMadeBetter.Client;
using Zw.MqttMadeBetter.Client.Auto;

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

//            var options = new MqttClientOptions
//            {
//                Endpoint = new MqttEndpoint(host, port),
//                ConnectionOptions =
//                {
//                    ClientId = Guid.NewGuid().ToString("N"),
//                    Credentials = new MqttCredentials(user, pass)
//                }
//            }.Freeze();

//            var client = await MqttClient.Create(options, CancellationToken.None);
//            client.ConnectionClosed += (o, e) =>
//            {
//                Console.WriteLine("CLOSED");
//                if (e != null)
//                    Console.WriteLine("{0}: {1}", e.GetType().Name, e.Message);
//            };
//            
//            await client.Send("mqtt/test/qos0/send", MqttMessageQos.QOS_0, ReadOnlyMemory<byte>.Empty, CancellationToken.None);
//            await client.Send("mqtt/test/qos1/send", MqttMessageQos.QOS_1, ReadOnlyMemory<byte>.Empty, CancellationToken.None);
//            await client.Send("mqtt/test/qos2/send", MqttMessageQos.QOS_2, ReadOnlyMemory<byte>.Empty, CancellationToken.None);
////
//            client.Messages.Subscribe(x => Console.WriteLine("Received " + Encoding.UTF8.GetString(x.Payload.Span)));
//            await client.Subscribe("mqtt/test/qos0", MqttMessageQos.QOS_0, CancellationToken.None);
//            await client.Subscribe("mqtt/test/qos1", MqttMessageQos.QOS_1, CancellationToken.None);
//            await client.Subscribe("mqtt/test/qos2", MqttMessageQos.QOS_2, CancellationToken.None);
//
//            Console.WriteLine("Running");
//            // await Task.Run(() => Console.ReadLine());
//            Console.ReadLine();
//            await client.Disconnect(CancellationToken.None);

            var options = new MqttAutoClientOptions().Freeze();

            var client = new MqttAutoClient(options);
            client.StateChanges.Subscribe(x => Console.WriteLine("State changed to {0}. Excep {1}: {2}", x.State,
                x.Exception?.GetType().Name, x.Exception?.Message));
            client.Messages.Subscribe(x => Console.WriteLine("Received: " + Encoding.UTF8.GetString(x.Payload.Span)));
            
            client.Subscribe("mqtt/test/qos0", MqttMessageQos.QOS_0);
            client.Subscribe("mqtt/test/qos1", MqttMessageQos.QOS_1);
            client.Subscribe("mqtt/test/qos2", MqttMessageQos.QOS_2);

            Console.WriteLine("Ready");

            bool toggle = false;
            while (true)
            {
                var x = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(x))
                {
                    if (!toggle)
                        client.Start(new MqttClientOptions()
                        {
                            Endpoint = new MqttEndpoint(host, port),
                            ConnectionOptions =
                            {
                                ClientId = Guid.NewGuid().ToString("N"),
                                Credentials = new MqttCredentials(user, pass)
                            },
                            AutoAcknowledge = true
                        }.Freeze());
                    else
                        client.Stop();

                    toggle = !toggle;
                }
                else
                {
                    if (x.StartsWith("!"))
                    {
                        client.Unsubscribe(x.Substring(1));
                    }
                    else
                    {
                        client.Subscribe(x, MqttMessageQos.QOS_0);
                    }
                }
            }
        }
    }
}