using System;
using Android.App;
using Android.Hardware;
using Android.OS;
using Android.Support.V7.App;
using Android.Things.Pio;
using Android.Views;
using Android.Widget;
using AndroidThingsHelloWorld.Helpers;
using Google.Android.Things.Contrib.Driver.Apa102;
using Google.Android.Things.Contrib.Driver.Bmx280;
using Google.Android.Things.Contrib.Driver.Button;
using Google.Android.Things.Contrib.Driver.Ht16k33;
using Google.Android.Things.Contrib.Driver.Pwmspeaker;
using Java.Lang;
using Button = Google.Android.Things.Contrib.Driver.Button.Button;
using Exception = System.Exception;
using Toolbar = Android.Support.V7.Widget.Toolbar;

namespace AndroidThingsHelloWorld.Activities
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity,ISensorCallback,ISensorEventListener
    {
        
        private TextView tempValueTxtView, pressureValueTxtView;
        private enum DisplayMode
        {
            TEMPERATURE,
            PRESSURE
        }

        private SensorManager sensorManager;
        private ButtonInputDriver buttonInputDriver;
        private Bmx280SensorDriver environmentalSensorDriver;
        private AlphanumericDisplay display;
        private DisplayMode displayMode = DisplayMode.TEMPERATURE;

        private Apa102 ledStrip;
        int [] rainbow=new int[7];
        private static  int LEDSTRIP_BRIGHTNESS = 1;
        private static  float BAROMETER_RANGE_LOW = 965f;
        private static  float BAROMETER_RANGE_HIGH = 1035f;
        private static  float BAROMETER_RANGE_SUNNY = 1010f;
        private static  float BAROMETER_RANGE_RAINY = 990f;

        private Gpio led;
        private int SPEAKER_READY_DELAY_MS = 300;
        private Speaker speaker;

        private float lastTemperature;
        private float lastPressure;
        
        
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            sensorManager = ((SensorManager) GetSystemService(SensorService));
            // GPIO button that generates 'A' keypresses (handled by onKeyUp method)
            try
            {
                buttonInputDriver = new ButtonInputDriver(BoardDefaults.GetButtonGPIOPin(),
                    Button.LogicState.PressedWhenLow,Convert.ToInt32(Keycode.A));
                buttonInputDriver.Register();
                Toast.MakeText(this, "Initialized GPIO Button that generates a keypress with KEYCODE_A", ToastLength.Long).Show();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw new RuntimeException("Error initializing GPIO button");
            }
            // I2C
            // Note: In this sample we only use one I2C bus, but multiple peripherals can be connected
            // to it and we can access them all, as long as they each have a different address on the
            // bus. Many peripherals can be configured to use a different address, often by connecting
            // the pins a certain way; this may be necessary if the default address conflicts with
            // another peripheral's. In our case, the temperature sensor and the display have
            // different default addresses, so everything just works.

            try
            {
                var callback=new DynamicSensorCallback(this);
                environmentalSensorDriver=new Bmx280SensorDriver(BoardDefaults.GetI2CBus());
                sensorManager.RegisterDynamicSensorCallback(callback);
                environmentalSensorDriver.RegisterTemperatureSensor();
                environmentalSensorDriver.RegisterPressureSensor();
                Toast.MakeText(this, "Initialized I2C BMP280", ToastLength.Long).Show();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw new RuntimeException("Error initializing BMP280");
            }

            try
            {
                display=new AlphanumericDisplay(BoardDefaults.GetI2CBus());
                display.SetEnabled(true);
                display.Clear();
                Toast.MakeText(this, "Initialized I2C Display", ToastLength.Long).Show();

            }
            catch (Exception e)
            {
                Toast.MakeText(this, "Error initializing displayy", ToastLength.Long).Show();
                Toast.MakeText(this, "Display disabled", ToastLength.Long).Show();
                display = null;
            }
            var toolbar = FindViewById<Toolbar>(Resource.Id.home_toolbar);
            SetSupportActionBar(toolbar);
            SupportActionBar.Title = "Hello World";

            tempValueTxtView = FindViewById<TextView>(Resource.Id.tempValue);
            pressureValueTxtView = FindViewById<TextView>(Resource.Id.pressureValue);

        }

        #region menu
        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.menu_main, menu);
            return true;
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            int id = item.ItemId;
            if (id == Resource.Id.action_settings)
            {
                return true;
            }

            return base.OnOptionsItemSelected(item);
        }
        #endregion


        

        public void OnDynamicSensorConnected(Sensor sensor)
        {
            if (sensor.GetType().ToString() == Sensor.StringTypeAmbientTemperature)
            {
                sensorManager.RegisterListener(this, sensor, SensorDelay.Normal);
            }
            else if (sensor.GetType().ToString() == Sensor.StringTypePressure)
            {
                sensorManager.RegisterListener(this, sensor, SensorDelay.Normal);
            }
        }

        public void OnDynamicSensorDisconnected(Sensor sensor)
        {

        }

        public void OnAccuracyChanged(Sensor sensor, SensorStatus accuracy)
        {
            Toast.MakeText(this,$"Accuracy Changed: {accuracy}",ToastLength.Long).Show();
        }

        public void OnSensorChanged(SensorEvent e)
        {
            switch (e.Sensor.Type.ToString())
            {
                case Sensor.StringTypeAmbientTemperature:
                    lastTemperature = e.Values[0];
                    Toast.MakeText(this, $"Sensor Changed: {lastTemperature}", ToastLength.Long).Show();
                    tempValueTxtView.Text = lastTemperature.ToString("##.##");
                    if (displayMode == DisplayMode.TEMPERATURE)
                        UpdateDisplay(lastTemperature);
                    break;
                case Sensor.StringTypePressure:
                    lastPressure = e.Values[0];
                    Toast.MakeText(this, $"Sensor Changed: {lastPressure}", ToastLength.Long).Show();
                    pressureValueTxtView.Text = lastPressure.ToString("##.##");
                    if (displayMode == DisplayMode.PRESSURE)
                        UpdateDisplay(lastPressure);
                    break;
                default:
                    break;
            }
        }

    

        private void UpdateDisplay(float value)
        {
            if (display == null) return;
            try
            {
                display.Display(value);
            }
            catch (Exception e)
            {
                Toast.MakeText(this,$"Error setting display",ToastLength.Long).Show();
            }
        }
    }
}

