using System;
using System.IO.Pipes;
using System.IO;
using System.Threading.Tasks;
using TikTokLiveSharp.Client;
using TikTokLiveSharp.Events;
using System.Threading;
namespace TikTokLiveSharpTestApplication
{
    internal class Program
    {
        // Declare the writer globally
        private static StreamWriter writer;
        private static SemaphoreSlim writeLock = new SemaphoreSlim(1, 1); // Allows only one thread to enter
        static async Task Main(string[] args)
        {
            using (var server = new NamedPipeServerStream("TestPipe"))
            {
                Console.WriteLine("Waiting for client connection...");
                await server.WaitForConnectionAsync();
                Console.WriteLine("Client connected.");

                using (writer = new StreamWriter(server) { AutoFlush = true })
                {
                    TikTokLiveClient client = new TikTokLiveClient("waveflaveyt", "");
                    client.OnLike += Client_OnLike;
                    client.OnGiftMessage += Client_OnGiftMessage;
                    client.OnFollow += Client_OnFollow;
                    client.Run(new System.Threading.CancellationToken());

                    // Wait for an exit command instead of reading messages from console
                    Console.WriteLine("Server is running. Type 'exit' to quit.");
                    string message;
                    do
                    {
                        message = Console.ReadLine();
                    } while (message?.ToLower() != "exit");
                }
            }
        }
        public class TikTokMessage
        {
            public string Type { get; set; }
            public object Data { get; set; }
        }

        private static async void Client_OnLike(TikTokLiveClient sender, Like e)
        {
            var message = new TikTokMessage
            {
                Type = "like",
                Data = new { e.Sender.UniqueId }
            };

            // Use Newtonsoft.Json for serialization
            string jsonMessage = Newtonsoft.Json.JsonConvert.SerializeObject(message);
            await SendMessageAsync(jsonMessage);
            Console.WriteLine(jsonMessage);
        }

        private static async void Client_OnGiftMessage(TikTokLiveClient sender, GiftMessage e)
        {
            
            var message = new TikTokMessage
            {
                Type = "gift",
                Data = new { e.User.UniqueId, e.Amount, GiftName = e.Gift.Name }
            };

            // Use Newtonsoft.Json for serialization
            string jsonMessage = Newtonsoft.Json.JsonConvert.SerializeObject(message);
            await SendMessageAsync(jsonMessage);
            Console.WriteLine(jsonMessage); // Debugging line
        }

        private static async void Client_OnFollow(TikTokLiveClient sender, Follow e)
        {

            var message = new TikTokMessage
            {
                Type = "follow",
                Data = new { e.User.UniqueId }
            };

            // Use Newtonsoft.Json for serialization
            string jsonMessage = Newtonsoft.Json.JsonConvert.SerializeObject(message);
            await SendMessageAsync(jsonMessage);
            Console.WriteLine(jsonMessage); // Debugging line
        }

        private static async Task SendMessageAsync(string message)
        {
            try
            {
                await writeLock.WaitAsync();
                if (writer != null && writer.BaseStream.CanWrite)
                {
                    await writer.WriteLineAsync(message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in writing to pipe: {ex.Message}");
            }
            finally
            {
                writeLock.Release();
            }
        }
    }
}