using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Android.Util;
using Newtonsoft.Json;
using Microsoft.Azure.Devices.Client;
using static AndroidThingsHelloWorld.Helpers.AppSettings;

namespace AndroidThingsHelloWorld.Helpers
{
    public static class IoTHubOps
    {
        private static DeviceClient deviceClient;
        public static void Initialise()
        {
            InitSettings();
            try
            {
                deviceClient = DeviceClient.CreateFromConnectionString(ConnectionString);
            }
            catch (Exception e)
            {
                Log.Error("",e.Message);
                throw;
            }
        }
        public static async Task  SendDeviceToCloudMessagesAsync(float temperature, float pressure)
        {
            try
            {
                // Create JSON message
                var payload = "{" +
                              "\"deviceId\":\"  imx7d_pico  \", " +
                              "\"temperature\":\"" + temperature + "\", " +
                              "\"pressure\":" + pressure + ", " +
                              "\"localTimestamp\":\"" + DateTime.Now.ToLocalTime() + "\"" +
                              "}";
           
                var message = new Message(Encoding.UTF8.GetBytes(payload));
                Debug.WriteLine("\t{0}> Sending message: [{1}]", DateTime.Now.ToLocalTime(), payload);

                // Send the telemetry message
                await deviceClient.SendEventAsync(message);
                Log.WriteLine(LogPriority.Info, "","Sent message");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("!!!! " + ex.Message);
                throw;
            }
        }
    }
}