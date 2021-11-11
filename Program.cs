using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;

namespace Testbot
{
    public class BotUser
    {
        [JsonProperty(PropertyName = "id")]
        public string Id => UserId.ToString();
        [JsonProperty(PropertyName = "pk")]
        public string Pk => Id;
        public long UserId { get; set; }
        public long ChatId { get; set; }
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

    public class Program
    {
        // Telegram 
        private static string BotToken = "<TG_BOT_TOKE>";

        // CosmosDb
        private const string EndpointUrl = "<AZURE COSMOSDB ENDPOINT>";
        private const string AuthorizationKey = "<AZURE COSMOSDB KEY>";
        private const string DatabaseId = "<AZURE COSMOSDB DB>";
        private const string ContainerId = "<AZURE COSMOSDB CONTAINER>";

        private static ChromeOptions? Options;
        private static ChromeDriver? Driver;
        private static bool IsStock = false;
        private static DateTime lastCheck = new DateTime();
        private static TelegramBotClient Bot = new TelegramBotClient(BotToken);
        private static CosmosClient cosmosClient = new CosmosClient(EndpointUrl, AuthorizationKey);

        static void InitOptions()
        {
            Options = new ChromeOptions();
            Options.AddArgument("--headless");
            Options.AddArgument("--no-sandbox");
            Options.AddArgument("--disable-dev-shm-usage");
            Options.AddArgument("--disable-gpu");
            Options.AddArgument("--use-gl=swiftshader");
            Options.AddArgument("--enable-webgl");
            Options.AddArgument("--ignore-gpu-blacklist");
            Options.AddArgument("--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/95.0.4638.69 Safari/537.36 Edg/95.0.1020.44");
        }

        static async Task<BotUser?> GetBotUserAsync(long userId)
        {
            try
            {
                var container = cosmosClient.GetContainer(DatabaseId, ContainerId);
                if (container != null)
                {
                    var response = await container.ReadItemAsync<BotUser>(userId.ToString(), new PartitionKey(userId.ToString()));
                    return response.Resource;
                }
                return default;
            }
            catch
            {
                return default;
            }
        }

        static async Task<List<BotUser>?> GetAllBotUserAsync()
        {
            try
            {
                var container = cosmosClient.GetContainer(DatabaseId, ContainerId);
                if (container != null)
                {
                    var list = new List<BotUser>();
                    var q = container.GetItemLinqQueryable<BotUser>();
                    var iterator = q.ToFeedIterator();
                    while (iterator.HasMoreResults)
                    {
                        var response = await iterator.ReadNextAsync();
                        list.AddRange(response.Resource);
                    }
                    return list;
                }
                return default;
            }
            catch
            {
                return default;
            }
        }

        static async Task SaveBotUserAsync(long userId, long chatId)
        {
            var botUser = new BotUser()
            {
                UserId = userId,
                ChatId = chatId
            };

            try
            {
                var container = cosmosClient.GetContainer(DatabaseId, ContainerId);
                if (container != null)
                {
                    await container.CreateItemAsync<BotUser>(botUser, new PartitionKey(botUser.Pk));
                }
            }
            catch(Exception ex)
            {
                Console.Write(ex.ToString());
            }
        }

        static async Task RemoveBotUserAsync(long userId)
        {
            try
            {
                var container = cosmosClient.GetContainer(DatabaseId, ContainerId);
                if (container != null)
                {
                    await container.DeleteItemAsync<BotUser>(userId.ToString(), new PartitionKey(userId.ToString()));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        static void InitDriver()
        {
            try
            {
                if (Driver != null)
                    Driver.Dispose();

                Driver = new ChromeDriver(Options);
                Driver.Manage().Window.Size = new System.Drawing.Size(1366, 768);
                Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
            }
            catch
            {
                Driver = null;
                InitDriver();
            }
        }

        static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                if (update.Message is Message message && !string.IsNullOrEmpty(update.Message.Text))
                {
                    if (await GetBotUserAsync(update.Message.From.Id) == null)
                    {
                        await SaveBotUserAsync(update.Message.From.Id, update.Message.Chat.Id);
                        await Bot.SendTextMessageAsync(update.Message.Chat.Id, "You've been registered for this (almost) real-time tracking bot!\nYou will be notified if there is any changes for Macbook Pro 2021 in Malaysia.\n\nSend /check to see current status.\nSend /stop to receiving notification");
                    }

                    if (update.Message.Text.StartsWith("/check"))
                    {
                        if (System.IO.File.Exists("current-page.jpg"))
                        {
                            var file = new FileStream("current-page.jpg", FileMode.Open);
                            await Bot.SendPhotoAsync(update.Message.Chat.Id, new Telegram.Bot.Types.InputFiles.InputOnlineFile(file), $"In stock: {IsStock} (Last check: {lastCheck.ToString("dd/MM/yyyy HH:mm")})");
                        }
                        else
                        {
                            await Bot.SendTextMessageAsync(update.Message.Chat.Id, $"In stock: {IsStock}");
                        }
                    }
                    else if (update.Message.Text.StartsWith("/stop"))
                    {
                        await RemoveBotUserAsync(update.Message.From.Id);
                        await Bot.SendTextMessageAsync(update.Message.Chat.Id, $"Done. Your account is removed from list");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            if (exception is ApiRequestException apiRequestException)
            {
                Console.WriteLine(apiRequestException.ToString());
            }

            return Task.CompletedTask;
        }

        static async Task FetchData()
        {
            while (true)
            {
                try
                {
                    if (Driver == null)
                        InitDriver();

                    Console.WriteLine("Fetching new data...");
                    Driver.Navigate().GoToUrl("https://www.apple.com/my/shop/buy-mac/macbook-pro/14-inch");
                    var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(5));
                    wait.Until(condition =>
                    {
                        try
                        {
                            var elementToBeDisplayed = Driver.FindElement(By.XPath("/html/body/div[2]/div[7]/div[2]/bundle-selection/store-provider/div[3]/div[2]/div[2]/div/div[2]/div/bundle-selector/div[1]/div[1]/div"));
                            return elementToBeDisplayed.Displayed;
                        }
                        catch (StaleElementReferenceException)
                        {
                            return false;
                        }
                        catch (NoSuchElementException)
                        {
                            return false;
                        }
                    });

                    IsStock = !Driver.PageSource.Contains("Check back later for availability");
                    Console.WriteLine($"{DateTime.Now} - {IsStock}");
                    lastCheck = DateTime.Now;

                    if (IsStock)
                    {
                        var list = await GetAllBotUserAsync();
                        if (list != null)
                            foreach (var user in list)
                            {
                                var file = new FileStream("current-page.jpg", FileMode.Open);
                                await Bot.SendPhotoAsync(user.ChatId, new Telegram.Bot.Types.InputFiles.InputOnlineFile(file), $"Status: In stock! (Last check: {lastCheck.ToString("dd/MM/yyyy HH:mm")})");
                                await RemoveBotUserAsync(user.UserId);
                                await Task.Delay(500);
                                await Bot.SendTextMessageAsync(user.ChatId, $"Your was removed from this bot. You'll no longer receive notification. Send any text to restart.");
                            }
                    }

                    Driver.GetScreenshot().SaveAsFile("current-page.jpg", ScreenshotImageFormat.Jpeg);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    InitDriver();
                }
                finally
                {
                    var randomTime = new Random().Next(5000, 60000);
                    Console.WriteLine($"Sleep for {randomTime}...");
                    await Task.Delay(randomTime);
                }
            }
        }

        static async Task Main()
        {
            Console.WriteLine("Starting...");

            InitOptions();
            InitDriver();

            var manualResetEvent = new ManualResetEvent(false);
            Bot = new TelegramBotClient(BotToken);

            _ = Task.Run(FetchData);

            var me = await Bot.GetMeAsync();
            Console.WriteLine($"Bot registered as: {me.Username}");

            using var cts = new CancellationTokenSource();

            Bot.StartReceiving(new DefaultUpdateHandler(HandleUpdateAsync, HandleErrorAsync), cts.Token);

            Console.WriteLine("Ready");

            manualResetEvent.WaitOne();
            cts.Cancel();
        }
    }
}