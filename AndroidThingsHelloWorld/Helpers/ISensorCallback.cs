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
    public interface ISensorCallback
    {
        void OnDynamicSensorConnected(Sensor sensor);
        void OnDynamicSensorDisconnected(Sensor sensor);
    }
}