using System;
using Android.Animation;
using Android.App;
using Android.Graphics;
using Android.Hardware;
using Android.OS;
using Android.Support.V7.App;
using Android.Things.Pio;
using Android.Util;
using Android.Views;
using Android.Views.Animations;
using Android.Widget;
using AndroidThingsHelloWorld.Helpers;
using Google.Android.Things.Contrib.Driver.Apa102;
using Google.Android.Things.Contrib.Driver.Bmx280;
using Google.Android.Things.Contrib.Driver.Button;
using Google.Android.Things.Contrib.Driver.Ht16k33;
using Google.Android.Things.Contrib.Driver.Pwmspeaker;
using Java.IO;
using Java.Lang;
using Button = Google.Android.Things.Contrib.Driver.Button.Button;
using Console = System.Console;
using Exception = System.Exception;
using Math = Java.Lang.Math;
using Toolbar = Android.Support.V7.Widget.Toolbar;

namespace AndroidThingsHelloWorld.Activities
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity, ISensorCallback, ISensorEventListener,
        ValueAnimator.IAnimatorUpdateListener, Animator.IAnimatorListener, IRunnable, IHandler
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
        int[] rainbow = new int[7];
        private static int LEDSTRIP_BRIGHTNESS = 1;
        private static float BAROMETER_RANGE_LOW = 965f;
        private static float BAROMETER_RANGE_HIGH = 1035f;
        private static float BAROMETER_RANGE_SUNNY = 1010f;
        private static float BAROMETER_RANGE_RAINY = 990f;

        private IGpio led;
        private int SPEAKER_READY_DELAY_MS = 300;
        private Speaker speaker;

        private float lastTemperature;
        private float lastPressure;

        private ValueAnimator slide;
        private DynamicSensorCallback callback;
        private readonly int MSG_UPDATE_BAROMETER_UI = 1;

        private Handlers mHandler;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            sensorManager = ((SensorManager) GetSystemService(SensorService));
            // GPIO button that generates 'A' keypresses (handled by onKeyUp method)
            try
            {
                buttonInputDriver = new ButtonInputDriver(BoardDefaults.GetButtonGPIOPin(),
                    Button.LogicState.PressedWhenLow, Convert.ToInt32(Keycode.A));
                buttonInputDriver.Register();
                Toast.MakeText(this, "Initialized GPIO Button that generates a keypress with KEYCODE_A",
                    ToastLength.Long).Show();
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
                environmentalSensorDriver = new Bmx280SensorDriver(BoardDefaults.GetI2CBus());
                callback = new DynamicSensorCallback(this);
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
                display = new AlphanumericDisplay(BoardDefaults.GetI2CBus());
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

            // SPI ledstrip
            try
            {
                ledStrip = new Apa102(BoardDefaults.GetSPIBus(), Apa102.Mode.Bgr)
                {
                    Brightness = LEDSTRIP_BRIGHTNESS
                };
                for (var i = 0; i < rainbow.Length; i++)
                {
                    float[] hsv = {i * 360f / rainbow.Length, 1.0f, 1.0f};
                    rainbow[i] = Color.HSVToColor(255, hsv);
                }
            }
            catch (IOException e)
            {
                ledStrip = null; //LED strip is optional
            }

            // GPIO led
            try
            {
                PeripheralManager pioManager = PeripheralManager.Instance;
                led = pioManager.OpenGpio(BoardDefaults.GetLEDGPIOPin());
                led.SetEdgeTriggerType(Gpio.EdgeNone);
                led.SetDirection(Gpio.DirectionOutInitiallyLow);
                led.SetActiveType(Gpio.ActiveHigh);
            }
            catch (IOException e)
            {
                Toast.MakeText(this, "Error initializing led", ToastLength.Short).Show();
            }


            // PWM speaker

            try
            {
                speaker = new Speaker(BoardDefaults.GetSpeakerPWMPin());
                slide = ValueAnimator.OfFloat(440, 440 * 4);
                slide.SetDuration(50);
                slide.RepeatCount = 5;
                slide.SetInterpolator(new LinearInterpolator());
                slide.AddUpdateListener(this);
                slide.AddListener(this);

                Handler handler = new Handler(MainLooper);
                handler.PostDelayed(new Runnable(Run), SPEAKER_READY_DELAY_MS);
            }
            catch (Exception e)
            {
                throw new RuntimeException("Error initializing speaker");
            }

            var toolbar = FindViewById<Toolbar>(Resource.Id.home_toolbar);
            SetSupportActionBar(toolbar);
            SupportActionBar.Title = "Hello World";

            tempValueTxtView = FindViewById<TextView>(Resource.Id.tempValue);
            pressureValueTxtView = FindViewById<TextView>(Resource.Id.pressureValue);

        }

        public override bool OnKeyDown(Keycode keyCode, KeyEvent e)
        {
            if (keyCode == Keycode.A)
            {
                displayMode = DisplayMode.PRESSURE;
                UpdateDisplay(lastPressure);
                try
                {
                    led.Value = true;
                }
                catch (IOException ex)
                {
                    throw ex;
                }

                return true;
            }

            return base.OnKeyDown(keyCode, e);
        }

        public override bool OnKeyUp(Keycode keyCode, KeyEvent e)
        {
            if (keyCode == Keycode.A)
            {
                displayMode = DisplayMode.TEMPERATURE;
                UpdateDisplay(lastTemperature);
                try
                {
                    led.Value = false;
                }
                catch (IOException ex)
                {
                    throw ex;
                }

                return true;
            }

            return base.OnKeyUp(keyCode, e);
        }

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
            Toast.MakeText(this, $"Accuracy Changed: {accuracy}", ToastLength.Long).Show();
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
                        UpdateBarometer(lastPressure);
                    break;
                default:
                    break;
            }
        }

        private void UpdateBarometer(float pressure)
        {
            // Update UI.
            if (!mHandler.HasMessages(MSG_UPDATE_BAROMETER_UI))
            {
                mHandler.SendEmptyMessageDelayed(MSG_UPDATE_BAROMETER_UI, 100);
            }

            // Update led strip.
            if (ledStrip == null)
            {
                return;
            }

            float t = (pressure - BAROMETER_RANGE_LOW) / (BAROMETER_RANGE_HIGH - BAROMETER_RANGE_LOW);
            int n = (int)Java.Lang.Math.Ceil(rainbow.Length * t);
            n = Math.Max(0, Math.Min(n, rainbow.Length));
            int[] colors = new int[rainbow.Length];
            for (int i = 0; i < n; i++)
            {
                int ri = rainbow.Length - 1 - i;
                colors[ri] = rainbow[ri];
            }

            try
            {
                ledStrip.Write(colors);
            }
            catch (IOException e)
            {
                e.PrintStackTrace();
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
                Toast.MakeText(this, $"Error setting display", ToastLength.Long).Show();
            }
        }

        public void OnAnimationUpdate(ValueAnimator animation)
        {
            try
            {
                var v = (float) animation.AnimatedValue;
                speaker.Play(v);
            }
            catch (IOException e)
            {
                throw new RuntimeException("Error sliding speaker", e);
            }
        }

        public void OnAnimationCancel(Animator animation)
        {

        }

        public void OnAnimationEnd(Animator animation)
        {
            try
            {
                speaker.Stop();
            }
            catch (IOException e)
            {
                throw new RuntimeException("Error sliding speaker", e);
            }
        }

        public void OnAnimationRepeat(Animator animation)
        {

        }

        public void OnAnimationStart(Animator animation)
        {
        }

        public void Run()
        {
            slide.Start();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            // Clean up sensor registrations
            sensorManager.UnregisterListener(this);
            sensorManager.UnregisterListener(this);

            // Clean up peripheral.
            if (environmentalSensorDriver != null)
            {
                try
                {
                    environmentalSensorDriver.Close();
                }
                catch (IOException e)
                {
                    e.PrintStackTrace();
                }

                environmentalSensorDriver = null;
            }

            if (buttonInputDriver != null)
            {
                try
                {
                    buttonInputDriver.Close();
                }
                catch (IOException e)
                {
                    e.PrintStackTrace();
                }

                buttonInputDriver = null;
            }

            if (display != null)
            {
                try
                {
                    display.Clear();
                    display.SetEnabled(false);
                    display.Close();
                }
                catch (IOException e)
                {
                    e.PrintStackTrace();
                }
                finally
                {
                    display = null;
                }
            }

            if (ledStrip != null)
            {
                try
                {
                    ledStrip.Brightness = 0;
                    ledStrip.Write(new int[7]);
                    ledStrip.Close();
                }
                catch (IOException e)
                {
                    e.PrintStackTrace();
                }
                finally
                {
                    ledStrip = null;
                }
            }

            if (led != null)
            {
                try
                {
                    led.Value = false;
                    led.Close();
                }
                catch (IOException e)
                {
                    e.PrintStackTrace();
                }
                finally
                {
                    led = null;
                }
            }
        }

        public void HandleMessage(Message msg)
        {
            if (msg.What == MSG_UPDATE_BAROMETER_UI)
            {
                //update ui
            }
        }
    }
}

