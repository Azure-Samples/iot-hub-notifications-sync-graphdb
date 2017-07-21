
namespace ThermostatDevice
{
    using System;
    using System.Configuration;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Common.Exceptions;
    using Microsoft.Azure.Devices.Shared;

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Press any key to exit");
            Console.WriteLine();

            MainAsync(args);

            Console.ReadKey();
        }

        static async Task MainAsync(string[] args)
        {
            try
            {
                string iotHubConnectionString = ConfigurationManager.AppSettings["IotHubConnectionString"];
                using (RegistryManager registryManager = RegistryManager.CreateFromConnectionString(iotHubConnectionString))
                {
                    await new Program().RunSampleAsync(registryManager);
                }
            }
            catch (IotHubException ex)
            {
                Console.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        async Task RunSampleAsync(RegistryManager registryManager)
        {
            string deviceId = ConfigurationManager.AppSettings["DeviceId"];

            // create a device client to emulated device sending reported properties updates
            Console.WriteLine("Create device client and connect to IoT Hub ...");
            Device device = await registryManager.GetDeviceAsync(deviceId);
            if (device == null)
            {
                Console.WriteLine($"Device {deviceId} not registered. Please register the device first.");
                return;
            }

            DeviceAuthenticationWithRegistrySymmetricKey authMethod = new DeviceAuthenticationWithRegistrySymmetricKey(
                    deviceId,
                    device.Authentication.SymmetricKey.PrimaryKey);

            Microsoft.Azure.Devices.Client.IotHubConnectionStringBuilder connectionStringBuilder = Microsoft.Azure.Devices.Client.IotHubConnectionStringBuilder.Create(
                "ailn-sample.azure-devices.net",
                authMethod);

            DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(connectionStringBuilder.ToString(), Microsoft.Azure.Devices.Client.TransportType.Mqtt);
            await deviceClient.OpenAsync();
            Console.WriteLine("Device connected");

            // update reported properties every 2 seconds
            while (true)
            {
                var prop = new TwinCollection();
                prop["temperature"] = new Random().Next(-100, 100);

                Console.WriteLine();
                Console.WriteLine($"Update reported properties:");
                Console.WriteLine(prop.ToJson(Newtonsoft.Json.Formatting.Indented));

                await deviceClient.UpdateReportedPropertiesAsync(prop);

                Console.WriteLine("Temperature updated");

                await Task.Delay(2000);
            }
        }
    }
}
