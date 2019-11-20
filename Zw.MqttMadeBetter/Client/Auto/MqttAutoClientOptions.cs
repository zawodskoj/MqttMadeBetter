using System;

namespace Zw.MqttMadeBetter.Client.Auto
{
    public delegate TimeSpan Backoff(int numOfRetry);
    
    public class MqttAutoClientOptions
    {
        public static Backoff ConstantBackoff(TimeSpan time) => _ => time;
        public static Backoff LinearBackoff(TimeSpan startTime, TimeSpan multiplier) => n => startTime + multiplier * n; 
        public static Backoff ExpBackoff(TimeSpan startTime, TimeSpan baseTime, TimeSpan maxTime) =>
            n =>
            {
                try
                {
                    var time = startTime + TimeSpan.FromMilliseconds(Math.Pow(baseTime.TotalMilliseconds, n));
                    return time > maxTime ? maxTime : time;
                }
                catch (OverflowException)
                {
                    return maxTime;
                }
            };

        public static Backoff ExpRandBackoff(TimeSpan startTime, TimeSpan baseTime, TimeSpan maxTime, TimeSpan randRange)
        {
            var rand = new Random();
            var expBackoff = ExpBackoff(startTime, baseTime, maxTime);

            return n => expBackoff(n) + (rand.NextDouble() * 2 - 1) * randRange;
        }

        public MqttClientOptions ClientOptions { get; } = new MqttClientOptions();
        public Backoff Backoff { get; set; } = ConstantBackoff(TimeSpan.FromSeconds(10));

        public MqttReadOnlyAutoClientOptions Freeze()
        {
            return new MqttReadOnlyAutoClientOptions(ClientOptions.Freeze(), Backoff);
        }
    }

    public class MqttReadOnlyAutoClientOptions
    {
        public MqttReadOnlyAutoClientOptions(MqttReadOnlyClientOptions clientOptions, Backoff backoff)
        {
            ClientOptions = clientOptions;
            
            if (!clientOptions.AutoAcknowledge)
                throw new ArgumentException("Disabled AutoAcknowledge is not supported in auto client", nameof(clientOptions));
                
            Backoff = backoff ?? throw new ArgumentNullException(nameof(backoff));
        }

        public MqttReadOnlyClientOptions ClientOptions { get; }
        public Backoff Backoff { get; }
    }
}