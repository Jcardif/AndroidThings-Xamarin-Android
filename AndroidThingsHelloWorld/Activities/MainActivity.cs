using System;
using Android.Animation;
using Android.App;
using Android.Graphics;
using Android.Hardware;
using Android.OS;
using Android.Support.V7.App;
using Android.Things.Pio;
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

        private TextView _tempValueTxtView, _pressureValueTxtView;

        private enum DisplayMode
        {
            TEMPERATURE,
            PRESSURE
        }

        private SensorManager _sensorManager;
        private ButtonInputDriver _buttonInputDriver;
        private Bmx280SensorDriver _environmentalSensorDriver;
        private AlphanumericDisplay _display;
        private DisplayMode _displayMode = DisplayMode.TEMPERATURE;

        private Apa102 _ledStrip;
        int[] rainbow = new int[7];
        private static int LEDSTRIP_BRIGHTNESS = 1;
        private static float BAROMETER_RANGE_LOW = 965f;
        private static float BAROMETER_RANGE_HIGH = 1035f;
        private static float BAROMETER_RANGE_SUNNY = 1010f;
        private static float BAROMETER_RANGE_RAINY = 990f;

        private IGpio _led;
        private int SPEAKER_READY_DELAY_MS = 300;
        private Speaker _speaker;

        private float _lastTemperature;
        private float _lastPressure;

        private ValueAnimator _slide;
        private DynamicSensorCallback _callback;
        private readonly int MSG_UPDATE_BAROMETER_UI = 1;

        private Handlers _mHandler;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            _mHandler=new Handlers(this);
            _sensorManager = ((SensorManager) GetSystemService(SensorService));
            // GPIO button that generates 'A' keypresses (handled by onKeyUp method)
            try
            {
                _buttonInputDriver = new ButtonInputDriver(BoardDefaults.GetButtonGPIOPin(),
                    Button.LogicState.PressedWhenLow, Convert.ToInt32(Keycode.A));
                _buttonInputDriver.Register();
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
                _environmentalSensorDriver = new Bmx280SensorDriver(BoardDefaults.GetI2CBus());
                _callback = new DynamicSensorCallback(this);
                _sensorManager.RegisterDynamicSensorCallback(_callback);
                _environmentalSensorDriver.RegisterTemperatureSensor();
                _environmentalSensorDriver.RegisterPressureSensor();
                Toast.MakeText(this, "Initialized I2C BMP280", ToastLength.Long).Show();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw new RuntimeException("Error initializing BMP280");
            }

            try
            {
                _display = new AlphanumericDisplay(BoardDefaults.GetI2CBus());
                _display.SetEnabled(true);
                _display.Clear();
                Toast.MakeText(this, "Initialized I2C Display", ToastLength.Long).Show();

            }
            catch (Exception)
            {
                Toast.MakeText(this, "Error initializing displayy", ToastLength.Long).Show();
                Toast.MakeText(this, "Display disabled", ToastLength.Long).Show();
                _display = null;
            }

            // SPI ledstrip
            try
            {
                _ledStrip = new Apa102(BoardDefaults.GetSPIBus(), Apa102.Mode.Bgr)
                {
                    Brightness = LEDSTRIP_BRIGHTNESS
                };
                for (var i = 0; i < rainbow.Length; i++)
                {
                    float[] hsv = {i * 360f / rainbow.Length, 1.0f, 1.0f};
                    rainbow[i] = Color.HSVToColor(255, hsv);
                }
            }
            catch (IOException)
            {
                _ledStrip = null; //LED strip is optional
            }

            // GPIO led
            try
            {
                PeripheralManager pioManager = PeripheralManager.Instance;
                _led = pioManager.OpenGpio(BoardDefaults.GetLEDGPIOPin());
                _led.SetEdgeTriggerType(Gpio.EdgeNone);
                _led.SetDirection(Gpio.DirectionOutInitiallyLow);
                _led.SetActiveType(Gpio.ActiveHigh);
            }
            catch (IOException)
            {
                Toast.MakeText(this, "Error initializing led", ToastLength.Short).Show();
            }


            // PWM speaker

            try
            {
                _speaker = new Speaker(BoardDefaults.GetSpeakerPWMPin());
                _slide = ValueAnimator.OfFloat(440, 440 * 4);
                _slide.SetDuration(50);
                _slide.RepeatCount = 5;
                _slide.SetInterpolator(new LinearInterpolator());
                _slide.AddUpdateListener(this);
                _slide.AddListener(this);

                Handler handler = new Handler(MainLooper);
                handler.PostDelayed(new Runnable(Run), SPEAKER_READY_DELAY_MS);
            }
            catch (Exception)
            {
                throw new RuntimeException("Error initializing speaker");
            }

            var toolbar = FindViewById<Toolbar>(Resource.Id.home_toolbar);
            SetSupportActionBar(toolbar);
            SupportActionBar.Title = "Hello World";

            _tempValueTxtView = FindViewById<TextView>(Resource.Id.tempValue);
            _pressureValueTxtView = FindViewById<TextView>(Resource.Id.pressureValue);

        }

        public override bool OnKeyDown(Keycode keyCode, KeyEvent e)
        {
            if (keyCode == Keycode.A)
            {
                _displayMode = DisplayMode.PRESSURE;
                UpdateDisplay(_lastPressure);
                _led.Value = true;

                return true;
            }

            return base.OnKeyDown(keyCode, e);
        }

        public override bool OnKeyUp(Keycode keyCode, KeyEvent e)
        {
            if (keyCode == Keycode.A)
            {
                _displayMode = DisplayMode.TEMPERATURE;
                UpdateDisplay(_lastTemperature);
                _led.Value = false;

                return true;
            }

            return base.OnKeyUp(keyCode, e);
        }

        public void OnDynamicSensorConnected(Sensor sensor)
        {
            if (sensor.GetType().ToString() == Sensor.StringTypeAmbientTemperature)
            {
                _sensorManager.RegisterListener(this, sensor, SensorDelay.Normal);
            }
            else if (sensor.GetType().ToString() == Sensor.StringTypePressure)
            {
                _sensorManager.RegisterListener(this, sensor, SensorDelay.Normal);
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
                    _lastTemperature = e.Values[0];
                    Toast.MakeText(this, $"Sensor Changed: {_lastTemperature}", ToastLength.Long).Show();
                    _tempValueTxtView.Text = _lastTemperature.ToString("##.##");
                    if (_displayMode == DisplayMode.TEMPERATURE)
                        UpdateDisplay(_lastTemperature);
                    break;
                case Sensor.StringTypePressure:
                    _lastPressure = e.Values[0];
                    Toast.MakeText(this, $"Sensor Changed: {_lastPressure}", ToastLength.Long).Show();
                    _pressureValueTxtView.Text = _lastPressure.ToString("##.##");
                    if (_displayMode == DisplayMode.PRESSURE)
                        UpdateBarometer(_lastPressure);
                    break;
            }
        }

        private void UpdateBarometer(float pressure)
        {
            // Update UI.
            if (!_mHandler.HasMessages(MSG_UPDATE_BAROMETER_UI))
            {
                _mHandler.SendEmptyMessageDelayed(MSG_UPDATE_BAROMETER_UI, 100);
            }

            // Update led strip.
            if (_ledStrip == null)
            {
                return;
            }

            float t = (pressure - BAROMETER_RANGE_LOW) / (BAROMETER_RANGE_HIGH - BAROMETER_RANGE_LOW);
            int n = (int)Math.Ceil(rainbow.Length * t);
            n = Math.Max(0, Math.Min(n, rainbow.Length));
            int[] colors = new int[rainbow.Length];
            for (int i = 0; i < n; i++)
            {
                int ri = rainbow.Length - 1 - i;
                colors[ri] = rainbow[ri];
            }

            try
            {
                _ledStrip.Write(colors);
            }
            catch (IOException e)
            {
                e.PrintStackTrace();
            }
        }


        private void UpdateDisplay(float value)
        {
            if (_display == null) return;
            try
            {
                _display.Display(value);
            }
            catch (Exception)
            {
                Toast.MakeText(this, $"Error setting display", ToastLength.Long).Show();
            }
        }

        public void OnAnimationUpdate(ValueAnimator animation)
        {
            try
            {
                var v = (float) animation.AnimatedValue;
                _speaker.Play(v);
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
                _speaker.Stop();
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
            _slide.Start();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            // Clean up sensor registrations
            _sensorManager.UnregisterListener(this);
            _sensorManager.UnregisterListener(this);

            // Clean up peripheral.
            if (_environmentalSensorDriver != null)
            {
                try
                {
                    _environmentalSensorDriver.Close();
                }
                catch (IOException e)
                {
                    e.PrintStackTrace();
                }

                _environmentalSensorDriver = null;
            }

            if (_buttonInputDriver != null)
            {
                try
                {
                    _buttonInputDriver.Close();
                }
                catch (IOException e)
                {
                    e.PrintStackTrace();
                }

                _buttonInputDriver = null;
            }

            if (_display != null)
            {
                try
                {
                    _display.Clear();
                    _display.SetEnabled(false);
                    _display.Close();
                }
                catch (IOException e)
                {
                    e.PrintStackTrace();
                }
                finally
                {
                    _display = null;
                }
            }

            if (_ledStrip != null)
            {
                try
                {
                    _ledStrip.Brightness = 0;
                    _ledStrip.Write(new int[7]);
                    _ledStrip.Close();
                }
                catch (IOException e)
                {
                    e.PrintStackTrace();
                }
                finally
                {
                    _ledStrip = null;
                }
            }

            if (_led != null)
            {
                try
                {
                    _led.Value = false;
                    _led.Close();
                }
                catch (IOException e)
                {
                    e.PrintStackTrace();
                }
                finally
                {
                    _led = null;
                }
            }
        }

        public void HandleMessage(Message msg)
        {
            if (msg.What == MSG_UPDATE_BAROMETER_UI)
            {
                if (_lastPressure > BAROMETER_RANGE_SUNNY)
                {

                }
                else if (_lastPressure < BAROMETER_RANGE_RAINY)
                {

                }
            }
        }
    }
}

