namespace ThermostatAdmin
{
    using System;
    using System.Configuration;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
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
            var device = new Device(deviceId);

            try
            {
                Console.WriteLine($"Add device '{deviceId}' ...");
                await registryManager.AddDeviceAsync(device);
                Console.WriteLine("Device added");
            }
            catch (IotHubException ex) when (ex.Code == ErrorCode.DeviceAlreadyExists)
            {
                Console.WriteLine("Device already exists. Use existing device ...");
                device = await registryManager.GetDeviceAsync(deviceId);
            }

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

            //// remove device
            //Console.WriteLine();
            //Console.WriteLine($"Remove device '{deviceId}' ...");
            //await Task.Delay(1000);
            //await registryManager.RemoveDeviceAsync(deviceId);
            //Console.WriteLine("Device removed");
        }

        static void PrintTwin(Twin twin)
        {
            twin.Properties.Desired = null; // for compact view as we only update tags

            Console.WriteLine();
            Console.WriteLine("============= Updated twin: ==================");
            Console.WriteLine(twin.ToJson(Newtonsoft.Json.Formatting.Indented));
            Console.WriteLine("==============================================");
        }
    }
}
