using HouseFinderWebBot.Api;
using HouseFinderWebBot.BotClient.Extensions;
using HouseFinderWebBot.Entities;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Newtonsoft.Json;
using Nito.AsyncEx;
using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;

namespace HouseFinderWebBot
{
    public class Shards
    {
        public int total { get; set; }
        public int successful { get; set; }
        public int skipped { get; set; }
        public int failed { get; set; }
    }

    public class Total
    {
        public int value { get; set; }
        public string relation { get; set; }
    }

    public class Source
    {
        public List<string> imageCaptionList { get; set; }
        public int area { get; set; }
        public bool forSale { get; set; }
        public string address { get; set; }
        public string city { get; set; }
        public int salePrice { get; set; }
        public string regionName { get; set; }
        public int rent { get; set; }
        public string type { get; set; }
        public bool forRent { get; set; }
        public int bedrooms { get; set; }
        public int iptuPlusCondominium { get; set; }
        public string visitStatus { get; set; }
        public string coverImage { get; set; }
        public int id { get; set; }
        public List<string> imageList { get; set; }
        public int parkingSpaces { get; set; }
        public int totalCost { get; set; }
        public List<string> activeSpecialConditions { get; set; }
    }

    public class Fields
    {
        public List<string> listingTags { get; set; }
    }

    public class Hit
    {
        public string _index { get; set; }
        public string _type { get; set; }
        public string _id { get; set; }
        public object _score { get; set; }
        public Source _source { get; set; }
        public Fields fields { get; set; }
        public List<object> sort { get; set; }
        public Total total { get; set; }
        public object max_score { get; set; }
        public List<Hit> hits { get; set; }
    }

    public class Root
    {
        public int took { get; set; }
        public bool timed_out { get; set; }
        public Shards _shards { get; set; }
        public Hits hits { get; set; }
        public string search_id { get; set; }
    }

    public class Hits
    {
        public Total total { get; set; }
        public decimal? max_score { get; set; }
        public List<Hit> hits { get; set; }
    }

    public interface IBotClientService
    {

    }

    public class BotClientService : IBotClientService
    {
        private static readonly HttpClient client = new HttpClient();
        public static ITelegramBotClient BotClient;
        public static List<string> ChatIds;
        public static Browser Browser;
        private readonly IOptions<AppConfiguration> config;

        public BotClientService(IOptions<AppConfiguration> config)
        {
            this.config = config;

            try
            {
                BotClient = new TelegramBotClient(config.Value.TelegramBotClientKey);
                ChatIds = config.Value.ChatIds.Split(";").ToList();

                // For Interval in Seconds 
                // This Scheduler will start at 11:10 and call after every 15 Seconds
                // IntervalInSeconds(start_hour, start_minute, seconds)
                // Eg.: MyScheduler.IntervalInSeconds(11, 10, 15,
                MyScheduler.IntervalInMinutes(DateTime.Now.Hour, DateTime.Now.Minute, 10,
                () =>
                {
                    try
                    {
                        Console.WriteLine($"====== Running scheduled job at: {DateTime.Now.ToString("HH:mm:ss")} ======");
                        AsyncContext.Run(CallPuppeteer);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                });

                MyScheduler.IntervalInSeconds(DateTime.Now.Hour, DateTime.Now.Minute, 60,
                () =>
                {
                    try
                    {
                        Console.WriteLine($"I'm alive!");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private static async Task SendNewApartmentsMessage(List<ApartmentInfo> apartments)
        {
            Console.WriteLine("Sending messages...");

            //Just a example
            BotClient.OnMessage += BotClient_OnMessage;
            BotClient.StartReceiving();

            await SendInitialMessage(apartments);

            //Sleep two seconds to prevent other messages 
            //coming before the initial message
            Thread.Sleep(1000);
            await SendApartmentsMessages(apartments);

            BotClient.StopReceiving();

            Console.WriteLine("Messages successfully sent");
        }

        private static async Task SendApartmentsMessages(List<ApartmentInfo> apartments)
        {
            foreach (var apartment in apartments)
            {
                Console.WriteLine("Sending apartment message...");

                foreach (var chatId in ChatIds)
                {
                    await BotClient.SendApartmentMessages(chatId, apartment);
                }
            }
        }

        private static async Task SendInitialMessage(List<ApartmentInfo> apartments)
        {
            foreach (var chatId in ChatIds)
            {
                await BotClient.SendInitialMessage(chatId, apartments);
            }
        }

        public List<ApartmentInfo> FilterNewApartments(List<ApartmentInfo> apartmentsFromPage)
        {
            var apartmentsFromPageIds = apartmentsFromPage.Select(w => w.Id).ToList();
            var client = new MongoClient(config.Value.MongoConnection);
            var database = client.GetDatabase("HouseFinder");
            IMongoCollection<ApartmentInfo> apartmentsCollection = database.GetCollection<ApartmentInfo>("ApartmentInfo");
            Console.WriteLine($"Total apartments on database: {apartmentsCollection.EstimatedDocumentCount()}");
            Expression<Func<ApartmentInfo, bool>> filter = x => apartmentsFromPageIds.Contains(x.Id);

            var apartmentsFromDb = apartmentsCollection.Find(filter).ToList();
            var apartmentsFromDbIds = apartmentsFromDb.Select(w => w.Id).ToList();

            var newApartments = apartmentsFromPage.Where(w => !apartmentsFromDbIds.Contains(w.Id)).ToList();
            InsertNewApartments(apartmentsCollection, newApartments);

            return newApartments;
        }

        private static void InsertNewApartments(IMongoCollection<ApartmentInfo> apartmentsCollection, List<ApartmentInfo> newApartments)
        {
            Console.WriteLine($"Inserting {newApartments.Count} new apartments");

            foreach (var newApartment in newApartments)
                apartmentsCollection.InsertOne(newApartment);
        }

        private async Task CallPuppeteer()
        {
            //Make a random request to prevent server to enter in idle state
            RandomRequest();

            Console.WriteLine("Getting apartments from page v2.0");
            var apartmentsFromPage = GetAparmentsFromApi();
            //List<ApartmentInfo> apartmentsFromPage = await GetAparmentsFromPage();

            Console.WriteLine("Comparing apartments and separating news");
            List<ApartmentInfo> newApartments = FilterNewApartments(apartmentsFromPage);
            newApartments = FilterApartments(newApartments);

            await SendNewApartmentsMessage(newApartments);
        }

        private static List<ApartmentInfo> GetAparmentsFromApi()
        {
            var apartments = new List<ApartmentInfo>();

            string filter = "{\"filters\":{\"map\":{\"bounds_north\":-19.871765235794022,\"bounds_south\":-19.961596764205975,\"bounds_east\":-43.88671987832523,\"bounds_west\":-43.982266121674755,\"center_lat\":-19.916681,\"center_lng\":-43.934493},\"cost\":{\"cost_type\":\"rent\",\"max_value\":3000,\"min_value\":500},\"availability\":\"any\",\"occupancy\":\"any\",\"sorting\":{\"criteria\":\"recent_rent\",\"order\":\"desc\"},\"page_size\":50,\"offset\":0},\"return\":[\"id\",\"coverImage\",\"rent\",\"totalCost\",\"salePrice\",\"iptuPlusCondominium\",\"area\",\"imageList\",\"imageCaptionList\",\"address\",\"regionName\",\"city\",\"visitStatus\",\"activeSpecialConditions\",\"type\",\"forRent\",\"forSale\",\"bedrooms\",\"parkingSpaces\",\"listingTags\"],\"business_context\":\"RENT\",\"user_id\":\"435dd27b-79aa-4663-a36a-10d7894401d9R\",\"context\":{\"search_filter\":{\"price_type\":\"rent\",\"price_min\":500,\"price_min_changed\":false,\"price_max\":3000,\"price_max_changed\":true,\"area_min\":20,\"area_min_changed\":false,\"area_max\":1000,\"area_max_changed\":false},\"map\":{\"bounds_north\":-19.871765235794022,\"bounds_south\":-19.961596764205975,\"bounds_east\":-43.88671987832523,\"bounds_west\":-43.982266121674755,\"center_lat\":-19.916681,\"center_lng\":-43.934493},\"search_dropdown_value\":\"Belo Horizonte, MG, Brasil\",\"dt\":\"2021-04-11T18:13:12.947Z\"}}";
            var content = new StringContent(filter);
            var response = client.PostAsync("https://www.quintoandar.com.br/api/yellow-pages/v2/search", content);
            var responseString = response.Result.Content.ReadAsStringAsync();

            Root newApartments = JsonConvert.DeserializeObject<Root>(responseString.Result);
            apartments.AddRange(MapApartmentsResultYellowPages(newApartments));

            return apartments;
        }

        private static IEnumerable<ApartmentInfo> MapApartmentsResultYellowPages(Root newApartments)
        {
            foreach (var apartment in newApartments.hits.hits)
            {
                yield return new ApartmentInfo()
                {
                    Id = apartment._source.id,
                    Aluguel = apartment._source.rent,
                    Area = apartment._source.area,
                    Total = apartment._source.totalCost,
                    Rua = apartment._source.address,
                    Bairro = apartment._source.regionName,
                    Cidade = apartment._source.city,
                    ImageRef = $"{ApartmentInfo.BaseUrl}/img/med/{apartment._source.coverImage}"
                };
            }
        }

        private static IEnumerable<ApartmentInfo> MapApartmentsResult(ApartmensApiResult apartmentsResult)
        {
            foreach (var apartment in apartmentsResult.Hits.Hit)
            {
                yield return new ApartmentInfo()
                {
                    Id = apartment.id,
                    Aluguel = apartment.fields.aluguel,
                    Area = apartment.fields.area,
                    Total = apartment.fields.custo,
                    Rua = apartment.fields.endereco,
                    Bairro = apartment.fields.regiaoNome,
                    Cidade = apartment.fields.cidade,
                    ImageRef = $"{ApartmentInfo.BaseUrl}/img/med/{apartment.fields.fotoCapa}"
                };
            }
        }

        private static void RandomRequest()
        {
            string url = @"https://housefinderinhan.herokuapp.com/fetch-data";
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.AutomaticDecompression = DecompressionMethods.GZip;
            request.GetResponse();
        }

        private static List<ApartmentInfo> FilterApartments(List<ApartmentInfo> newApartments)
        {
            var excludeBairroFromFilter = new List<string>() {
                "Cinqüentenário, Belo Horizonte",
                "Nova Granada, Belo Horizonte",
            };

            newApartments = newApartments
               .Where(w => !excludeBairroFromFilter.Contains(w.Bairro))
               .ToList();

            newApartments = newApartments
                .Where(w => w.Total <= 3000)
                .ToList();

            return newApartments;
        }

        private static async Task<List<ApartmentInfo>> GetAparmentsFromPage()
        {
            Console.WriteLine($"Current memory usage: {Process.GetCurrentProcess().PrivateMemorySize64 / Math.Pow(1024, 2)}mb");
            Console.WriteLine("Downloading chromium");

            await DowloadChromium();
            Console.WriteLine("Launching chromium :|");

            Browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                DefaultViewport = new ViewPortOptions()
                {
                    Width = 1368,
                    Height = 768,
                },
                Args = new[] { "--no-sandbox --disable-setuid-sandbox" }
            });

            Browser.Disconnected += Browser_OnDisconnect;
            Console.WriteLine("Chromium OK! Searching apartments on page");

            // Create a new page and go to Bing Maps
            Page page = await Browser.NewPageAsync();
            await page.GoToAsync("https://www.quintoandar.com.br/");

            await ScrollPageToBottom(page);

            ////Wait for cities combobox load then click
            await page.WaitForSelectorAsync(".rsmey3-0");
            await page.ClickAsync(".rsmey3-0");

            await page.WaitForTimeoutAsync(1000);

            ////Wait for cities to load then click
            await page.WaitForSelectorAsync(@"[data-value=""belo-horizonte-mg-brasil""]");
            await page.ClickAsync(@"[data-value=""belo-horizonte-mg-brasil""]");

            ////Wait apartments to load
            await page.WaitForTimeoutAsync(1000);

            ////Slides the apartments to right until the button is disabled
            await SlideApartmentsToRight(page);

            await page.WaitForTimeoutAsync(1000);

            List<ApartmentInfo> apartmentsFromPage = await GetApartmentsFromHtml(page);
            Browser.Disconnect();

            var apartmentsFromBH = apartmentsFromPage.Where(w => w.Bairro.Contains("Belo Horizonte")).ToList();
            var others = apartmentsFromPage.Where(w => !w.Bairro.Contains("Belo Horizonte")).ToList();

            return apartmentsFromBH;
        }

        private static void Browser_OnDisconnect(object sender, EventArgs e)
        {
            Thread.Sleep(100);
            Process[] chromeInstances = Process.GetProcessesByName("chromium");

            foreach (Process p in chromeInstances)
                p.Kill();
        }

        private static async Task<List<ApartmentInfo>> GetApartmentsFromHtml(Page page)
        {
            return await page.EvaluateExpressionAsync<List<ApartmentInfo>>(@"Array.from(getApartmentsInfo()).map(a => a);
                                                 function getApartmentsInfo() {
                                                      let apartments = [];
                                                      let elements = document.getElementsByClassName('eFOTEL')[1].getElementsByClassName('teva8h-2');
                                                      for (let element of elements)
                                                      {
                                                        var linkEl = element.getElementsByClassName('fHkake')[0];
                                                        var ruaEl = element.getElementsByClassName('falbBb')[0];
                                                        var bairroEl = element.getElementsByClassName('Ongdx')[0];
                                                        var areaEl = element.getElementsByClassName('ivMPuZ')[0];
                                                        var aluguelEl = element.getElementsByClassName('dfcRZz')[0];
                                                        var totalEl = element.getElementsByClassName('WCcfX')[0];
                                                        var imageEl = element.getElementsByTagName('img')[0];

                                                        if (linkEl == null || 
                                                            ruaEl == null || 
                                                            bairroEl == null ||
                                                            areaEl == null ||
                                                            aluguelEl == null ||
                                                            totalEl == null ||
                                                            imageEl == null 
                                                           ) continue;

                                                        apartments.push({ 
                                                          href: linkEl.href,
                                                          rua: ruaEl.innerText,
                                                          bairro: bairroEl.innerText,
                                                          area: areaEl.innerText,
                                                          aluguel: aluguelEl.innerText,
                                                          total: totalEl.innerText,
                                                          imageRef: imageEl.src                                                
                                                        });                                                                   
                                                      };

                                                      return apartments;
                                                 }");
        }

        private static async Task SlideApartmentsToRight(Page page)
        {
            var rightButton = @"document.getElementsByClassName('eFOTEL')[1].querySelector('[right]')";

            while (await page.EvaluateExpressionAsync<bool>(rightButton + ".disabled") != true)
            {
                await page.EvaluateExpressionAsync(rightButton + ".click()");
                await page.WaitForTimeoutAsync(250);
            }
        }

        private static async Task ScrollPageToBottom(Page page)
        {
            await page.EvaluateExpressionAsync(@"new Promise((resolve, reject) => {
                  var totalHeight = 0;
                  var distance = 100;
                  var timer = setInterval(() => {
                    var scrollHeight = document.body.scrollHeight;
                    window.scrollBy(0, distance);
                    totalHeight += distance;

                    if (totalHeight >= scrollHeight) {
                      clearInterval(timer);
                      resolve();
                    }
                  }, 100);
                });");
        }

        //Check if chromium is downloaded and download if not
        private static async Task DowloadChromium()
        {
            await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);
        }

        private static async void BotClient_OnMessage(object sender, Telegram.Bot.Args.MessageEventArgs e)
        {
            if (e.Message.Text != null)
            {
                Console.WriteLine($"Received a text message in chat {e.Message.Chat.Id}.");

                await BotClient.SendTextMessageAsync(
                    chatId: e.Message.Chat,
                    text: "You said:\n" + e.Message.Text
                );
            }
        }
    }
}
