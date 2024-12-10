using System.Net.Sockets;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;

class Program
{
    static string access_token = "bTU3LUdPLU5aNEZka21ZOHRqYWk0QzNmblZVcm43a2N1clB3T056TU1oST0";
    private const int ServerPort = 8888;

    static async Task Main()
    {
        Console.WriteLine("Выберите режим работы программы:");
        Console.WriteLine("1 - Запустить сервер");
        Console.WriteLine("2 - Запустить клиент");
        var choice = Console.ReadLine();

        if (choice == "1")
        {
            await StartServer();
        }
        else if (choice == "2")
        {
            await StartClient();
        }
        else
        {
            Console.WriteLine("Неверный выбор. Попробуйте снова.");
        }
    }

    // Сервер
    static async Task StartServer()
    {
        var listener = new TcpListener(IPAddress.Any, ServerPort);
        listener.Start();
        Console.WriteLine("Сервер запущен. Ожидание подключений...");

        while (true)
        {
            var client = await listener.AcceptTcpClientAsync();
            Console.WriteLine("Подключение от клиента.");

            _ = Task.Run(async () =>
            {
                using (var stream = client.GetStream())
                {
                    var buffer = new byte[1024];
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    var ticker = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    Console.WriteLine($"Получен запрос для тикера: {ticker}");
                    var price = await GetLastPriceFromDatabase(ticker);

                    var response = price != null
                        ? $"Последняя цена для {ticker}: {price}"
                        : $"Данных для тикера {ticker} нет.";

                    buffer = Encoding.UTF8.GetBytes(response);
                    await stream.WriteAsync(buffer, 0, buffer.Length);
                }
            });
        }
    }

    // Клиент
    static async Task StartClient()
    {
        Console.WriteLine("Введите тикер:");
        var ticker = Console.ReadLine();

        using (var client = new TcpClient())
        {
            try
            {
                await client.ConnectAsync("127.0.0.1", ServerPort);
                using (var stream = client.GetStream())
                {
                    var buffer = Encoding.UTF8.GetBytes(ticker);
                    await stream.WriteAsync(buffer, 0, buffer.Length);

                    buffer = new byte[1024];
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    Console.WriteLine($"Ответ от сервера: {response}");
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Ошибка подключения: {ex.Message}");
            }
        }
    }

    static async Task<decimal?> GetLastPriceFromDatabase(string tickerSymbol)
    {
        using (var context = new AppDbContext())
        {
            var ticker = await context.Ticker.FirstOrDefaultAsync(t => t.TickerName == tickerSymbol);
            if (ticker == null)
            {
                return null;
            }

            var lastPrice = await context.Prices
                .Where(p => p.TickerId == ticker.Id)
                .OrderByDescending(p => p.Date)
                .FirstOrDefaultAsync();

            return lastPrice?.Price;
        }
    }
}

public class AppDbContext : DbContext
{
    public DbSet<Tickers> Ticker { get; set; }
    public DbSet<Prices> Prices { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer("Server=localhost,1433;Database=StockMarket;User Id=SA;Password=YourStrong!Password;Encrypt=False;TrustServerCertificate=True;");
    }
}

public class Tickers
{
    public int Id { get; set; }
    public string TickerName { get; set; }
}

public class Prices
{
    public int Id { get; set; }
    public int TickerId { get; set; }
    public decimal Price { get; set; }
    public DateTime Date { get; set; }
}
