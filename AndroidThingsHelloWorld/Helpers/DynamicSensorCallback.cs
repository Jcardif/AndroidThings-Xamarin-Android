using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Hardware;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace AndroidThingsHelloWorld.Helpers
{
    public class DynamicSensorCallback : SensorManager.DynamicSensorCallback
    {
        private ISensorCallback sensorCallback;

        public DynamicSensorCallback(ISensorCallback sensorCallback)
        {
            this.sensorCallback = sensorCallback;
        }

        public override void OnDynamicSensorConnected(Sensor sensor)
        {
            base.OnDynamicSensorConnected(sensor);
            sensorCallback.OnDynamicSensorConnected(sensor);
        }

        public override void OnDynamicSensorDisconnected(Sensor sensor)
        {
            base.OnDynamicSensorDisconnected(sensor);
            sensorCallback.OnDynamicSensorDisconnected(sensor);
        }
    }
}