using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO.Pipes;
using GTA;
using GTA.Native;
using GTA.UI;
using GTA.Math;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Newtonsoft.Json;




public class FirstGTAMOD : Script // Change "YouTubeTutorial" to the name of your program.
{

    Random rnd = new Random();
    Vector3 rampBottom = new Vector3(-3682.0f, -1898.5f,0.0f);  //Bottom Ramp Location Var
    Vector3 realRampBottom = new Vector3(-3682.38f, -1898.76f, 14.0f); //Actual Bottom of Ramp
    static Vector3 realRampTop = new Vector3(-3682.38f, -1290.5f, 489.0f); 
    Vector3 playerPosition = new Vector3();
    Vector3 playerSphere = new Vector3();
    Vector3 vehicleSpawn2 = new Vector3();
    Vector3 pedSpawn = new Vector3();
    Vector3 zeroV = new Vector3();
    Vector3 finishMarkerPos = new Vector3(-3682.5f, -1377.24f, 430.7f);
    Vector3 finishMarkerSc = new Vector3(15.0f, 5.0f, 15.0f);
    static float sRadius = 17f;  //Radius of Big Ramp Bottom Sphere
    static float s2Radius = 15f;  //Radius of Small Player Sphere
    Vector3 sphereS = new Vector3(s2Radius, s2Radius, s2Radius); //Debug Sphere Dimensions
    Vector3 sphereB = new Vector3(sRadius, sRadius, sRadius);
    Vector3 startPosition = new Vector3(realRampTop.X, realRampTop.Y - 10, realRampTop.Z); // Starting position for the chain of explosions
    float distanceBetweenExplosionsY = 12f; // Distance between each explosion
    float distanceBetweenExplosionsZ = 9.5f; // Distance between each explosion
    int numberOfExplosions = 50; // Total number of explosions in the chain
    private ConcurrentQueue<string> messageQueue;
    private Dictionary<Ped, string> pedNames = new Dictionary<Ped, string>();
    private DateTime nextExplosionTime = DateTime.MinValue;
    private int currentExplosionIndex = 0;
    private bool sequenceRunning = false;
    private int countdown = 5; // Initial countdown time
    private string exploderName;
    public FirstGTAMOD() // Change "YouTubeTutorial" to the name of your program.
    {
        messageQueue = new ConcurrentQueue<string>();
        InitializeModels();


        Tick += OnTick;
        KeyUp += OnKeyUp;
        KeyDown += OnKeyDown;

    }



    private void InitializeModels()
    {
        var modelsYouAreUsing = new HashSet<Model>() { VehicleHash.Hakuchou2, 1265391242, VehicleHash.Mower, VehicleHash.Tractor3 };
        foreach (var model in modelsYouAreUsing)
        {
            model.Request();
        }
        while (!modelsYouAreUsing.All(x => x.IsLoaded))
        {
            Wait(0);
        }
    }
    private bool isConnected = false;
    private NamedPipeClientStream client;
    private StreamReader reader;



    private async void OnTick(object sender, EventArgs e)
    {


        //Drawing Ped Names and Removing
        try
        {
            DrawNamesAbovePeds();
        }
        catch (Exception ex)
        {
            Notification.Show($"Tick Error: {ex.Message}");
        }

        List<Ped> toRemove = new List<Ped>();
        foreach (var ped in pedNames.Keys)
        {
            if (!ped.Exists()) // Check if the Ped no longer exists
            {
                toRemove.Add(ped);
            }
        }
        foreach (var ped in toRemove)
        {
            pedNames.Remove(ped);
        }



        //Sequencing for Explosion
        if (sequenceRunning)
        {
            // Handle the countdown
            if (countdown > 0 && DateTime.Now >= nextExplosionTime)
            {
                GTA.UI.Screen.ShowSubtitle($"~r~" +exploderName + "~w~ initiated ~r~Destruction Event~w~ in: ~r~"+countdown);
                countdown--;
                nextExplosionTime = DateTime.Now.AddSeconds(1); // Set the next countdown tick
            }
            // Start the explosions after the countdown
            else if (countdown <= 0)
            {
                if (DateTime.Now >= nextExplosionTime)
                {
                    if (currentExplosionIndex < numberOfExplosions)
                    {
                        Vector3 explosionPosition = startPosition - new Vector3(0f, currentExplosionIndex * distanceBetweenExplosionsY, currentExplosionIndex * distanceBetweenExplosionsZ);
                        World.AddExplosion(explosionPosition.Around(3), ExplosionType.Blimp2, 100.0f, 1.0f);
                        nextExplosionTime = DateTime.Now.AddMilliseconds(75); // Delay for next explosion
                        currentExplosionIndex++;
                    }
                    else
                    {
                        // Sequence complete
                        sequenceRunning = false;
                    }
                }
            }
        }

            //Messege Queue
            try
        {
            ProcessMessageQueue();
        }
        catch (Exception ex)
        {
            Notification.Show($"Tick Error: {ex.Message}");
        }
        //PIPE STUFF PIPE STUFF PIPE STUFF PIPE STUFF PIPE STUFF PIPE STUFF PIPE STUFF PIPE STUFF PIPE STUFF PIPE STUFF 
        if (!isConnected && Game.IsKeyPressed(Keys.F12)) // Example: Press F12 to connect
        {
            isConnected = true; // Prevent re-entry
            Notification.Show("Connecting to server...");

            await ConnectToServer();

            // Start reading messages from the server in a separate task
            _ = Task.Run(ReadServerMessages);

        }




        //GAME STUFF GAME STUFF GAME STUFF GAME STUFF GAME STUFF GAME STUFF GAME STUFF GAME STUFF GAME STUFF GAME STUFF GAME STUFF 
        playerPosition = Game.Player.Character.Position;
        playerSphere = new Vector3(playerPosition.X, playerPosition.Y - 40.0f, playerPosition.Z - 35.0f); //Delete area behind player location
        if (World.GetClosestVehicle(rampBottom, sRadius) == null) //Checks if vihicle in area and delete if it is
        {
            //do nothing
        }
        else
        {
            World.GetClosestVehicle(rampBottom, sRadius).Delete();
        }
        // World.DrawMarker(MarkerType.DebugSphere, rampBottom, zeroV, zeroV, sphereB, Color.Red);
        if (World.GetClosestVehicle(playerSphere, s2Radius) == null) //Checks if vihicle in area and delete if it is
        {
            //do nothing
        }
        else
        {
            World.GetClosestVehicle(playerSphere, s2Radius).Delete();
        }
        // World.DrawMarker(MarkerType.DebugSphere, playerSphere, zeroV, zeroV, sphereS, Color.Red);   //Draws Sphere of Vehicle Deletion


        //Finish Line Code-----------------------------------------------------------
        World.DrawMarker(MarkerType.CheckeredFlagRect, finishMarkerPos, new Vector3(0f, 0f, 0f), zeroV, finishMarkerSc, Color.Aqua, true); //Finish Flag Marker

        if (playerPosition.Y > -1377.0f)          //after X=-151 Telerport to ramp bottom and play win sound
        {
            Game.Player.Character.Position = realRampBottom;

            System.Media.SoundPlayer player = new System.Media.SoundPlayer(@"C:\Users\Hyper\source\repos\FirstGTAMOD\FirstGTAMOD\Audio\finish3.wav");
            player.Play();
            World.AddExplosion(realRampBottom + new Vector3(5f,5f,0f), ExplosionType.Flare, 1.0f, 1.0f);
            World.AddExplosion(realRampBottom + new Vector3(-5f, 5f, 0f), ExplosionType.Flare, 1.0f, 1.0f);
            PlayCelebrationAnimation("anim@mp_player_intcelebrationmale@air_guitar", "air_guitar");
        }

        if (playerPosition.Z < 10)          //Floor teleport zone
        {
            Game.Player.Character.Position = realRampBottom;
            System.Media.SoundPlayer player = new System.Media.SoundPlayer(@"C:\Users\Hyper\source\repos\FirstGTAMOD\FirstGTAMOD\Audio\lose.wav");
            player.Play();
        }

    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {




        //Vehicle Spawn Offset
        vehicleSpawn2 = new Vector3(playerPosition.X, playerPosition.Y + 120.0f, playerPosition.Z + 100.0f);
        if (e.KeyCode == Keys.F10) // If 'F10' is pressed, execute the following code below.
        {
            // Creating a vehicle object and telling it to spawn .
            SpawnMultipleSameVehicle(VehicleHash.Rhino, 10);
            Notification.Show("Your car has ~g~spawned~w~! Enjoy!");
        }

        if (e.KeyCode == Keys.F11) // If 'F10' is pressed, execute the following code below.
        {
            Ped followPed = World.CreatePed(PedHash.Babyd, pedSpawn);
            if (followPed != null && !pedNames.ContainsKey(followPed)) // Check if Ped is valid and not already in the dictionary
            {
                pedNames.Add(followPed, "Dummy");
            }
            if (followPed != null && followPed.Exists())
            {
                // Define the point to which the pedestrian should run
                Vector3 runToPoint = pedSpawn + new Vector3(0, 1000, 0); // 50 units in front of the spawn location

                // Instruct the pedestrian to run to the specified point
                followPed.Task.RunTo(runToPoint, false);
            }
            
        }

        if (e.KeyCode == Keys.L) // Deletes all vehicles
        {
            DeleteAllVehicles();
        }


        if (e.KeyCode == Keys.K)
        {
            StartExplosionSequence();

        }
         
        if (e.KeyCode == Keys.J)
        {
            DeleteAllPeds();

        }

        if (e.KeyCode == Keys.H)
        {
            PlayCelebrationAnimation("anim@mp_player_intcelebrationmale@air_guitar", "air_guitar");

        }

    }

    private void OnKeyUp(object sender, KeyEventArgs e)
    {
        
    }

    private void PlayCelebrationAnimation(string animationDictionary, string animationName)
    {
        // Replace these with the actual names of the animation dictionary and animation

        // Ensure the animation dictionary is loaded
        Function.Call(Hash.REQUEST_ANIM_DICT, animationDictionary);
        while (!Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, animationDictionary))
        {
            Script.Wait(100);
        }

        // Get the player character (protagonist)
        Ped player = Game.Player.Character;

        // Play the animation
        // Flags: 0 for normal, 1 to loop, etc. See GTAV scripting documentation for more details
        Function.Call(Hash.TASK_PLAY_ANIM, player, animationDictionary, animationName, 8.0f, -8.0f, -1, 0, 0, false, false, false);

        // Optionally, unload the animation dictionary if it's no longer needed
        Function.Call(Hash.REMOVE_ANIM_DICT, animationDictionary);
    }

    public void StartExplosionSequence()
    {
        if (!sequenceRunning)
        {
            System.Media.SoundPlayer player = new System.Media.SoundPlayer(@"C:\Users\Hyper\source\repos\FirstGTAMOD\FirstGTAMOD\Audio\nuke.wav");
            player.Play();
            sequenceRunning = true;
            nextExplosionTime = DateTime.Now.AddSeconds(1); // Start countdown
            currentExplosionIndex = 0;
            countdown = 5; // Reset countdown
        }
    }

    private void ProcessMessageQueue()
    {
        pedSpawn = new Vector3(playerPosition.X, playerPosition.Y + 5.0f, playerPosition.Z + 5.0f);
        vehicleSpawn2 = new Vector3(playerPosition.X, playerPosition.Y + 120.0f, playerPosition.Z + 100.0f);
        while (messageQueue.TryDequeue(out string messageJson))
        {
            try
            {
                var message = JsonConvert.DeserializeObject<TikTokMessage>(messageJson);

                switch (message.Type)
                {
                    case "like":
                        var likeData = message.Data.ToObject<LikeData>();
                        var likeTimestamp = DateTime.Now.ToString("HH:mm:ss");
                        Notification.Show($"¦~y~{likeData.UserId} ~r~liked~w~ at {likeTimestamp}!");
                        Vehicle vehicle01 = World.CreateVehicle(1265391242, vehicleSpawn2.Around(1), rnd.Next());
                        vehicle01.Mods.CustomPrimaryColor = Color.Red;

                        break;

                    case "follow":
                        var followData = message.Data.ToObject<LikeData>();
                        var followTimestamp = DateTime.Now.ToString("HH:mm:ss");
                        Notification.Show($"~y~{followData.UserId} ~b~Followed~w~ at {followTimestamp}!");
                        Ped followPed = World.CreatePed(PedHash.Babyd, pedSpawn);
                        if (followPed != null && !pedNames.ContainsKey(followPed)) // Check if Ped is valid and not already in the dictionary
                        {
                            pedNames.Add(followPed, followData.UserId);
                        }
                        if (followPed != null && followPed.Exists())
                        {
                            // Define the point to which the pedestrian should run
                            Vector3 runToPoint = pedSpawn + new Vector3(0, 1000, 0); // 1000 units in front of the spawn location

                            // Instruct the pedestrian to run to the specified point
                            followPed.Task.RunTo(runToPoint, false);
                        }

                        break;

                    case "gift":
                        var giftData = message.Data.ToObject<GiftData>();
                        Notification.Show($"~y~{giftData.UserId ?? "Unknown user"}~w~ sent ~g~{giftData.Amount}x {giftData.GiftName}!");
                        if (giftData.GiftName == "Rose")
                        {
                            SpawnMultipleSameVehicle(VehicleHash.Mower, giftData.Amount);
                        }
                        if (giftData.GiftName == "Finger Heart")
                        {
                            followPed = World.CreatePed(PedHash.Babyd, pedSpawn);
                            if (followPed != null && !pedNames.ContainsKey(followPed)) // Check if Ped is valid and not already in the dictionary
                            {
                                pedNames.Add(followPed, giftData.UserId);
                            }
                            if (followPed != null && followPed.Exists())
                            {
                                // Define the point to which the pedestrian should run
                                Vector3 runToPoint = pedSpawn + new Vector3(0, 1000, 0); // 1000 units in front of the spawn location

                                // Instruct the pedestrian to run to the specified point
                                followPed.Task.RunTo(runToPoint, false);
                            }
                        }
                        if (giftData.GiftName == "Doughnut")
                        {
                            SpawnMultipleSameVehicle(VehicleHash.Rhino, 10);
                        }
                        if (giftData.GiftName == "Cap")
                        {
                            StartExplosionSequence();
                            exploderName = giftData.UserId;
                        }
                        else if (giftData.GiftName != null && giftData.GiftName != "Rose")
                        {
                            SpawnMultipleSameVehicle(VehicleHash.Mower, giftData.Amount);
                        }
                        break;
                    default:
                        Notification.Show($"Unknown message type: {message.Type}");
                        break;
                }
            }
            catch (JsonException ex)
            {
                Notification.Show($"JSON Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Notification.Show($"Error processing message: {ex.Message}");
            }
        }
    }

    private async Task ConnectToServer()
    {
        client = new NamedPipeClientStream(".", "TestPipe", PipeDirection.InOut);
        try
        {
            client.Connect(5000); // Timeout after 5000 ms
            reader = new StreamReader(client);
            Notification.Show("Connected to server.");
        }
        catch (Exception ex)
        {
            Notification.Show($"Failed to connect: {ex.Message}");
            isConnected = false;
        }
    }

    private async Task ReadServerMessages()
    {
        try
        {
            while (isConnected && client != null && client.IsConnected)
            {
                string serverMessage = await reader.ReadLineAsync();
                if (serverMessage == null)
                {
                    Notification.Show("Server has disconnected."); // Consider marshaling back to main thread if necessary
                    isConnected = false;
                    break;
                }

                messageQueue.Enqueue(serverMessage);
            }
        }
        catch (Exception ex)
        {
            // Since we're in a background task, directly using game API here might not be safe
            // Consider flagging an error state and handling it in OnTick instead
            Console.WriteLine($"Error reading from server: {ex.Message}");
            isConnected = false;
        }
        finally
        {
            reader?.Dispose();
            client?.Dispose();
        }
    }

    public void DrawTextAbovePed(Ped ped, string text)
    {
        // Convert the world position of the ped's head to screen coordinates
        Vector3 worldPosition = ped.Position + new Vector3(0, 0, 1.0f); // Adjust Z-axis as needed
        PointF success = GTA.UI.Screen.WorldToScreen(worldPosition, true);

        if (success != null)
        {
            float distance = Game.Player.Character.Position.DistanceTo(ped.Position);

            // Determine a scale for the text size based on the distance
            float scale = .50f - (distance / 70.0f); // Adjust the divisor to scale appropriately
            scale = Math.Max(0.1f, Math.Min(scale, 1.0f));
            // Draw text at the converted screen position
            var textElement = new TextElement(text, success, scale, Color.White, GTA.UI.Font.ChaletLondon, GTA.UI.Alignment.Center,false,true);

            // Draw the text element
            textElement.Draw();
        }
    }

    private void DeleteAllPeds()
    {
        // Get all peds in the world
        Ped[] allPeds = World.GetAllPeds();

        foreach (Ped ped in allPeds)
        {
            // Check if the ped is not the player character
            if (!ped.IsPlayer)
            {
                ped.Delete(); // Delete the ped
            }
        }
    }

    private void DrawNamesAbovePeds()
    {
        foreach (KeyValuePair<Ped, string> entry in pedNames)
        {
            DrawTextAbovePed(entry.Key, entry.Value);
        }
    }

    //CAR STUFF CAR STUFF CAR STUFF CAR STUFF CAR STUFF CAR STUFF CAR STUFF CAR STUFF CAR STUFF 
    void DeleteAllVehicles()
    {
        // Get all vehicles in the world
        Vehicle[] allVehicles = World.GetAllVehicles();

        // Loop through each vehicle
        foreach (Vehicle vehicle in allVehicles)
        {
            // Check if the vehicle exists to avoid errors
            if (vehicle.Exists())
            {
                // Delete the vehicle
                vehicle.Delete();
            }
        }
    }


    void MakeVehicleIndestructible(Vehicle vehicle)
    {
        // Set vehicle to be indestructible: no bullet damage, fire, explosion, etc.
        vehicle.CanBeVisiblyDamaged = false;
        vehicle.IsBulletProof = true;
        vehicle.IsFireProof = true;
        vehicle.IsExplosionProof = false;
        vehicle.IsCollisionProof = true;
        vehicle.IsMeleeProof = true;

        // Optional: Set vehicle health to maximum
        vehicle.EngineHealth = 1000; // Maximum engine health
        vehicle.PetrolTankHealth = 1000; // Maximum petrol tank health
        vehicle.BodyHealth = 1000; // Maximum body health

    }

    private void SpawnMultipleSameVehicle(VehicleHash modelToSpawn, int count)
    {
        vehicleSpawn2 = new Vector3(playerPosition.X, playerPosition.Y + 140.0f, playerPosition.Z + 115.0f); // Get the player's current position

        Model vehicleModel = new Model(modelToSpawn);
 

        if (vehicleModel.IsInCdImage && vehicleModel.IsValid)
        {
            for (int i = 0; i < count; i++)
            {
                // Calculate a slightly different spawn position for each vehicle to prevent overlapping
                Vector3 spawnPosition = vehicleSpawn2.Around(2 + (i * 1)); // This will space out the vehicles a bit

                // Spawn the vehicle at the calculated position
                Vehicle newVehicle = World.CreateVehicle(vehicleModel, spawnPosition);

                // Optionally, customize the new vehicle here (e.g., color, invincibility)
                newVehicle.IsInvincible = true; // Example: Make the vehicle invincible
                newVehicle.ApplyForce(new Vector3(0, -1.4f, -1f) * 100f);
            }

            vehicleModel.MarkAsNoLongerNeeded(); // Cleanup the model from memory after spawning
        }
        else
        {
            Notification.Show("Vehicle model not found."); // Notify the player if the model wasn't loaded properly
        }
    }


    private void SpawnMultipleSameObjects(string modelToSpawn, int count)
    {
        vehicleSpawn2 = new Vector3(playerPosition.X, playerPosition.Y + 90.0f, playerPosition.Z + 100.0f); // Get the player's current position

        Model vehicleModel = new Model(modelToSpawn);
       // vehicleModel.Request(500);

        if (vehicleModel.IsInCdImage && vehicleModel.IsValid)
        {
            for (int i = 0; i < count; i++)
            {
                // Calculate a slightly different spawn position for each vehicle to prevent overlapping
                Vector3 spawnPosition = vehicleSpawn2.Around(1 + (i * 1)); // This will space out the vehicles a bit

                // Spawn the vehicle at the calculated position
                Prop newObject = World.CreateProp(vehicleModel, spawnPosition, true, false);

                // Optionally, customize the new vehicle here (e.g., color, invincibility)
                //newObject.IsInvincible = true;
            }

            //vehicleModel.MarkAsNoLongerNeeded(); // Cleanup the model from memory after spawning
        }
        else
        {
            Notification.Show("Vehicle model not found."); // Notify the player if the model wasn't loaded properly
        }
    }

    public class TikTokMessage
    {
        public string Type { get; set; }
        public Newtonsoft.Json.Linq.JToken Data { get; set; } // Flexible container for any JSON structure
    }

    public class LikeData
    {
        [JsonProperty("uniqueId")]
        public string UserId { get; set; }
    }

    public class GiftData
    {
        [JsonProperty("uniqueId")]
        public string UserId { get; set; }

        [JsonProperty("amount")]
        public int Amount { get; set; }

        [JsonProperty("giftName")]
        public string GiftName { get; set; }
    }

}
