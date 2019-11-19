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
            
            var test = await MqttChannel.Open(host, port, x => { }, CancellationToken.None);
            LoopRecv(test);
            await test.Send(new MqttConnectControlPacket(
                    Guid.NewGuid().ToString("N"),
                    user,
                    pass,
                    null,
                    true,
                    5),
                CancellationToken.None);

            await Task.Delay(200);

            await test.Send(new MqttSubscribeControlPacket(
                    1,
                    new[] {new TopicFilter("mqtt/test", MqttMessageQos.QOS_1),}),
                CancellationToken.None);

            while (true)
            {
                await test.Send(new MqttPingreqControlPacket(), CancellationToken.None);
                await Task.Delay(2500);
            }
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