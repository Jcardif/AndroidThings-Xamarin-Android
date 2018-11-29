using Android.OS;
using Java.Lang;

namespace AndroidThingsHelloWorld.Helpers
{
    public static class BoardDefaults
    {
        private static readonly string DEVICE_RPI3 = "rpi3";
        private static readonly string DEVICE_IMX6UL_PICO = "imx6ul_pico";
        private static readonly string DEVICE_IMX7D_PICO = "imx7d_pico";

        public static string GetButtonGPIOPin()
        {
            if (Build.Device == DEVICE_RPI3)
                return "BCM21";
            if (Build.Device == DEVICE_IMX6UL_PICO)
            {
                return "GPIO2_IO03";
            }

            if (Build.Device == DEVICE_IMX7D_PICO)
            {
                return "GPIO6_IO14";
            }

            throw new IllegalArgumentException("Unknown device: " + Build.Device);
        }

        public static string GetLEDGPIOPin()
        {
            if (Build.Device == DEVICE_RPI3)
                return "BCM6";
            if (Build.Device == DEVICE_IMX6UL_PICO)
            {
                return "GPIO4_IO22";
            }

            if (Build.Device == DEVICE_IMX7D_PICO)
            {
                return "GPIO2_IO02";
            }

            throw new IllegalArgumentException("Unknown device: " + Build.Device);
        }

        public static string GetI2CBus()
        {
            if (Build.Device == DEVICE_RPI3)
                return "I2C1";
            if (Build.Device == DEVICE_IMX6UL_PICO)
            {
                return "I2C2";
            }

            if (Build.Device == DEVICE_IMX7D_PICO)
            {
                return "I2C1";
            }

            throw new IllegalArgumentException("Unknown device: " + Build.Device);
        }

        public static string GetSPIBus()
        {
            if (Build.Device == DEVICE_RPI3)
                return "SPI0.0";
            if (Build.Device == DEVICE_IMX6UL_PICO)
            {
                return "SPI3.0";
            }

            if (Build.Device == DEVICE_IMX7D_PICO)
            {
                return "SPI3.1";
            }

            throw new IllegalArgumentException("Unknown device: " + Build.Device);
        }
        public static string GetSpeakerPWMPin()
        {
            if (Build.Device == DEVICE_RPI3)
                return "PWM1";
            if (Build.Device == DEVICE_IMX6UL_PICO)
            {
                return "PWM8";
            }

            if (Build.Device == DEVICE_IMX7D_PICO)
            {
                return "PWM2";
            }
            throw new IllegalArgumentException("Unknown device: " + Build.Device);
        }
    }
}