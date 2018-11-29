using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace AndroidThingsHelloWorld.Helpers
{
    public class Handlers : Handler
    {
        private IHandler handler;

        public Handlers(IHandler handler)
        {
            this.handler = handler;
        }

        public override void HandleMessage(Message msg)
        {
            base.HandleMessage(msg);
            handler.HandleMessage(msg);
        }
    }
}