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

        // usage: 
        // add <deviceId>
        // location <deviceId> <building> <floor> <room>
        // temperature <deviceId> <temperature>
        // delete <deviceId>
        static bool ParseArguments(string[] args, out string action, out string deviceId, out string building, out string floor, out string room, out string temperature)
        {
            action = deviceId = building = floor = room = temperature = null;
            if (args.Length < 2)
            {
                PrintHelp("Wrong number of arguments.");
                return false;
            }

            action = args[0];
            switch (action)
            {
                case "add":
                    if (args.Length != 2)
                    {
                        PrintHelp("Wrong number of arguments.");
                        return false;
                    }
                    deviceId = args[1];
                    break;
                case "location":
                    if (args.Length != 5)
                    {
                        PrintHelp("Wrong number of arguments.");
                        return false;
                    }
                    deviceId = args[1];
                    building = args[2];
                    floor = args[3];
                    room = args[4];
                    break;
                case "temperature":
                    if (args.Length != 3)
                    {
                        PrintHelp("Wrong number of arguments.");
                        return false;
                    }
                    deviceId = args[1];
                    temperature = args[2];
                    break;
                case "delete":
                    if (args.Length != 2)
                    {
                        PrintHelp("Wrong number of arguments.");
                        return false;
                    }
                    deviceId = args[1];
                    break;
                default:
                    PrintHelp($"Unrecognized action '{action}'.");
                    return false;
            }

            return true;
        }

        static void PrintHelp(string message)
        {
            Console.WriteLine(message);
            Console.WriteLine("usage: ThermostatAdmin add <deviceId>");
            Console.WriteLine("                       location <deviceId> <building> <floor> <room>");
            Console.WriteLine("                       temperature <temperature>");
            Console.WriteLine("                       delete <deviceId>");
            Console.WriteLine();
            Console.WriteLine("deviceId    - id of a thermostat to add/update/delete");
            Console.WriteLine("building    - building to update a thermostat location");
            Console.WriteLine("floor       - floor to update a thermostat location");
            Console.WriteLine("room        - room to update a thermostat location");
            Console.WriteLine("temperature - set desired temperature of a thermostat");
        }

        static async Task MainAsync(string[] args)
        {
            string action, deviceId, building, floor, room, temperature;

            if (!ParseArguments(args, out action, out deviceId, out building, out floor, out room, out temperature))
            {
                return;
            }

            try
            {
                string iotHubConnectionString = ConfigurationManager.AppSettings["IotHubConnectionString"];
                using (RegistryManager registryManager = RegistryManager.CreateFromConnectionString(iotHubConnectionString))
                {
                    await new Program().RunSampleAsync(registryManager, action, deviceId, building, floor, room, temperature);
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

        Task RunSampleAsync(RegistryManager registryManager, string action, string deviceId, string building, string floor, string room, string temperature)
        {
            switch (action)
            {
                case "add":
                    return AddThermostatAsync(registryManager, deviceId);
                case "location":
                    return UpdateLocationAsync(registryManager, deviceId, building, floor, room);
                case "temperature":
                    return SetDesiredTemperatureAsync(registryManager, deviceId, temperature);
                case "delete":
                    return DeleteThermostatAsync(registryManager, deviceId);
                default:
                    throw new NotSupportedException($"Action {action} no supported");
            }
        }

        async Task AddThermostatAsync(RegistryManager registryManager, string deviceId)
        {
            var device = new Device(deviceId);

            Console.WriteLine($"Add thermostat '{deviceId}' ...");
            await registryManager.AddDeviceAsync(device);
            Console.WriteLine("Thermostat added");

            Twin thermostat = await registryManager.GetTwinAsync(deviceId);
            PrintTwin(thermostat);
        }

        async Task UpdateLocationAsync(RegistryManager registryManager, string deviceId, string building, string floor, string room)
        {
            Console.WriteLine($"Get thermostat '{deviceId}' ...");
            Twin thermostat = await registryManager.GetTwinAsync(deviceId);
            if (thermostat == null)
            {
                Console.WriteLine($"Thermostat {deviceId} not found. Nothing to update.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Update location:");

            var twin = new Twin(deviceId);
            twin.Tags["location"] = new TwinCollection();
            twin.Tags["location"]["building"] = building;
            twin.Tags["location"]["floor"] = floor;
            twin.Tags["location"]["room"] = room;

            Console.WriteLine(twin.Tags.ToJson(Newtonsoft.Json.Formatting.Indented));

            twin = await registryManager.UpdateTwinAsync(deviceId, twin, thermostat.ETag);
            PrintTwin(twin);
        }

        async Task SetDesiredTemperatureAsync(RegistryManager registryManager, string deviceId, string temperature)
        {
            Console.WriteLine($"Get thermostat '{deviceId}' ...");
            Twin thermostat = await registryManager.GetTwinAsync(deviceId);
            if (thermostat == null)
            {
                Console.WriteLine($"Thermostat {deviceId} not found. Nothing to update.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Set desired temperature:");

            var twin = new Twin(deviceId);
            twin.Properties.Desired["temperature"] = temperature;

            Console.WriteLine(twin.Properties.Desired.ToJson(Newtonsoft.Json.Formatting.Indented));

            twin = await registryManager.UpdateTwinAsync(deviceId, twin, thermostat.ETag);
            PrintTwin(twin);
        }

        async Task DeleteThermostatAsync(RegistryManager registryManager, string deviceId)
        {
            Console.WriteLine($"Get thermostat '{deviceId}' ...");
            Twin thermostat = await registryManager.GetTwinAsync(deviceId);
            if (thermostat == null)
            {
                Console.WriteLine($"Thermostat {deviceId} not found. Nothing to delete.");
                return;
            }

            await registryManager.RemoveDeviceAsync(deviceId);
            Console.WriteLine("Thermostat removed successfully");
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
