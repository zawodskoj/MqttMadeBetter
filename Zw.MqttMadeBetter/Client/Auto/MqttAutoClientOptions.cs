using System;
using Microsoft.Extensions.Logging;

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
                    var time = startTime + TimeSpan.FromSeconds(Math.Pow(baseTime.TotalSeconds, n));
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

        public Backoff Backoff { get; set; } = ConstantBackoff(TimeSpan.FromSeconds(10));

        public MqttReadOnlyAutoClientOptions Freeze()
        {
            return new MqttReadOnlyAutoClientOptions(Backoff);
        }
    }

    public class MqttReadOnlyAutoClientOptions
    {
        public MqttReadOnlyAutoClientOptions(Backoff backoff)
        {
            Backoff = backoff ?? throw new ArgumentNullException(nameof(backoff));
        }
        
        public Backoff Backoff { get; }
    }
}