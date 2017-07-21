
namespace ThermostatDevice
{
    using System;
    using System.Configuration;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Common.Exceptions;
    using Microsoft.Azure.Devices.Shared;

    using Device = Microsoft.Azure.Devices.Client;
    using Service = Microsoft.Azure.Devices;

    class Program
    {
        static void Main(string[] args)
        {
            MainAsync(args).Wait();

            Console.WriteLine();
            Console.WriteLine("Done. Press any key to exit");
            Console.ReadKey();
        }

        // usage: <deviceId> <temperature>
        static bool ParseArguments(string[] args, out string deviceId, out string temperature)
        {
            deviceId = temperature = null;
            if (args.Length != 2)
            {
                PrintHelp("Wrong number of arguments.");
                return false;
            }

            deviceId = args[0];
            temperature = args[1];

            return true;
        }

        static void PrintHelp(string message)
        {
            Console.WriteLine(message);
            Console.WriteLine("usage: ThermostatDevice <deviceId> <temperature>");
            Console.WriteLine();
            Console.WriteLine("deviceId    - id of a thermostat");
            Console.WriteLine("temperature - temperature to report");
        }

        static async Task MainAsync(string[] args)
        {
            string deviceId, temperature;

            if (!ParseArguments(args, out deviceId, out temperature))
            {
                return;
            }

            try
            {
                await new Program().RunSampleAsync(ConfigurationManager.AppSettings["IotHubConnectionString"], deviceId, temperature);
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

        async Task RunSampleAsync(string iotHubConnectionString, string deviceId, string temperature)
        {
            var iotHubConnectionStringBuilder = Service.IotHubConnectionStringBuilder.Create(iotHubConnectionString);
            using (RegistryManager registryManager = RegistryManager.CreateFromConnectionString(iotHubConnectionString))
            {
                // create a device client to emulate therostat sending temperature update
                Console.WriteLine("Create device client and connect to IoT Hub ...");
                Service.Device device = await registryManager.GetDeviceAsync(deviceId);
                if (device == null)
                {
                    Console.WriteLine($"Thermostat {deviceId} not registered. Please register the thermostat first.");
                    return;
                }

                var authMethod = new DeviceAuthenticationWithRegistrySymmetricKey(deviceId, device.Authentication.SymmetricKey.PrimaryKey);
                var connectionStringBuilder = Device.IotHubConnectionStringBuilder.Create(iotHubConnectionStringBuilder.HostName, authMethod);
                DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(connectionStringBuilder.ToString(), Device.TransportType.Mqtt);

                await deviceClient.OpenAsync();
                Console.WriteLine("Thermostat connected");

                var prop = new TwinCollection();
                prop["temperature"] = temperature;

                Console.WriteLine();
                Console.WriteLine($"Update reported properties:");
                Console.WriteLine(prop.ToJson(Newtonsoft.Json.Formatting.Indented));

                await deviceClient.UpdateReportedPropertiesAsync(prop);

                Console.WriteLine("Temperature updated");
            }
        }
    }
}
