namespace UpdateTwinApp
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Common.Exceptions;
    using Microsoft.Azure.Devices.Shared;

    class Program
    {
        static void Main(string[] args)
        {
            MainAsync(args).Wait();

            Console.WriteLine();
            Console.WriteLine("Done. Press any key to exit");
            Console.ReadKey();
        }

        static async Task MainAsync(string[] args)
        {
            try
            {
                string iotHubConnectionString = "HostName=ailn-sample.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=w9ybyIkcgxTQ+wfty9fsoyZ6RRK+27XMPS6CUPrySOE=";
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
            // generate unique device id and add device to iot hub
            string deviceId = $"sample-{Stopwatch.GetTimestamp().ToString("x")}";
            var device = new Device(deviceId);

            Console.WriteLine($"Add device '{deviceId}' ...");
            await registryManager.AddDeviceAsync(device);
            Console.WriteLine("Device added");

            // create a device client to emulated device sending reported properties updates
            Console.WriteLine();
            Console.WriteLine("Create device client and connect to IoT Hub ...");
            DeviceClient deviceClient = await ConnectDeviceClientAsync(deviceId, registryManager);
            Console.WriteLine("Device connected");

            // set initial device location
            var twin = new Twin(deviceId);
            twin.Tags["location"] = new TwinCollection();
            twin.Tags["location"]["building"] = "43";
            twin.Tags["location"]["floor"] = "1";
            twin.Tags["location"]["room"] = "1R";

            Console.WriteLine();
            Console.WriteLine("Update device location:");
            Console.WriteLine(twin.Tags.ToJson(Newtonsoft.Json.Formatting.Indented));

            await Task.Delay(1000);
            twin = await registryManager.UpdateTwinAsync(deviceId, twin, etag: "*");
            PrintTwin(twin);

            // update reported properties
            await this.UpdateReportedPropertiesAsync(deviceId, deviceClient);

            // change room
            twin = new Twin(deviceId);
            twin.Tags["location"] = new TwinCollection();
            twin.Tags["location"]["room"] = "1S";

            Console.WriteLine();
            Console.WriteLine("Change room from 1R to 1S:");
            Console.WriteLine(twin.Tags.ToJson(Newtonsoft.Json.Formatting.Indented));

            await Task.Delay(1000);
            twin = await registryManager.UpdateTwinAsync(deviceId, twin, etag: "*");
            PrintTwin(twin);

            // update reported properties
            await this.UpdateReportedPropertiesAsync(deviceId, deviceClient);

            // change floor
            twin = new Twin(deviceId);
            twin.Tags["location"] = new TwinCollection();
            twin.Tags["location"]["floor"] = "2";
            twin.Tags["location"]["room"] = "2S";

            Console.WriteLine();
            Console.WriteLine("Change floor from 1 to 2:");
            Console.WriteLine(twin.Tags.ToJson(Newtonsoft.Json.Formatting.Indented));

            await Task.Delay(1000);
            twin = await registryManager.UpdateTwinAsync(deviceId, twin, etag: "*");
            PrintTwin(twin);

            // update reported properties
            await this.UpdateReportedPropertiesAsync(deviceId, deviceClient);

            // remove device
            Console.WriteLine();
            Console.WriteLine($"Remove device '{deviceId}' ...");
            await Task.Delay(1000);
            await registryManager.RemoveDeviceAsync(deviceId);
            Console.WriteLine("Device removed");
        }

        static void PrintTwin(Twin twin)
        {
            twin.Properties.Desired = null; // for compact view as we only update tags

            Console.WriteLine();
            Console.WriteLine("============= Updated twin: ==================");
            Console.WriteLine(twin.ToJson(Newtonsoft.Json.Formatting.Indented));
            Console.WriteLine("==============================================");
        }

        static async Task<DeviceClient> ConnectDeviceClientAsync(string deviceId, RegistryManager registryManager)
        {
            var device = await registryManager.GetDeviceAsync(deviceId);

            DeviceAuthenticationWithRegistrySymmetricKey authMethod = new DeviceAuthenticationWithRegistrySymmetricKey(
                    deviceId,
                    device.Authentication.SymmetricKey.PrimaryKey);

            Microsoft.Azure.Devices.Client.IotHubConnectionStringBuilder connectionStringBuilder = Microsoft.Azure.Devices.Client.IotHubConnectionStringBuilder.Create(
                "ailn-sample.azure-devices.net",
                authMethod);

            DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(connectionStringBuilder.ToString(), Microsoft.Azure.Devices.Client.TransportType.Mqtt);
            await deviceClient.OpenAsync();
            return deviceClient;
        }

        async Task UpdateReportedPropertiesAsync(string deviceId, DeviceClient deviceClient)
        {
            var prop = new TwinCollection();
            prop["temperature"] = new Random().Next(-100, 100);

            Console.WriteLine();
            Console.WriteLine($"Update reported properties:");
            Console.WriteLine(prop.ToJson(Newtonsoft.Json.Formatting.Indented));

            await deviceClient.UpdateReportedPropertiesAsync(prop);

            Console.WriteLine("Temperature updated");
        }
    }
}
