using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Polling;
using Microsoft.VisualBasic;

class Program
{
    private static readonly string token = ""; // Replace with your token
    private static readonly string connectionString = "Data Source=.db"; // Your SQLite database

    static async Task Main(string[] args)
    {
        // Initialize the database
        InitializeDatabase();

        // Fetch all users
        await FetchAllUsers();

        var botClient = new TelegramBotClient(token);
        var cts = new CancellationTokenSource();

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = { } // Receive all types of updates
        };

        botClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions, cts.Token);

        Console.WriteLine("Bot is running. Press Enter to exit.");
        Console.ReadLine();

        cts.Cancel();
    }

    private static async Task FetchAllUsers()
    {
        using (var connection = new SqliteConnection(connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT UserId, UniqueKey FROM Users";

            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    long userId = reader.GetInt64(0);
                    string uniqueKey = reader.GetString(1);
                    Console.WriteLine($"UserId: {userId}, UniqueKey: {uniqueKey}");
                }
            }
        }
    }

    private static void InitializeDatabase()
    {
        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Users (
                Id INTEGER PRIMARY KEY,
                UserId INTEGER NOT NULL,
                UniqueKey TEXT NOT NULL
            )";
            command.ExecuteNonQuery();
        }
    }

    // Class to store data
    public class MyData
    {
        public long UserId { get; set; }
        public string UniqueKey { get; set; }
    }

    public static async Task InsertData(MyData data)
    {
        using (var connection = new SqliteConnection(connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO Users (UserId, UniqueKey) VALUES (@userId, @uniqueKey)";
            command.Parameters.AddWithValue("@userId", data.UserId);
            command.Parameters.AddWithValue("@uniqueKey", data.UniqueKey);

            int rowsAffected = await command.ExecuteNonQueryAsync();
            Console.WriteLine($"{rowsAffected} rows inserted.");
        }
    }

    private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Type == Telegram.Bot.Types.Enums.UpdateType.Message && update.Message?.Text != null)
        {
            Console.WriteLine($"Received message: {update.Message.Text}");

            if (update.Message.Text.Equals("/start", StringComparison.OrdinalIgnoreCase))
            {
                // Check if the user has used the start command before
                if (await UserHasKey(update.Message.From.Id))
                {
                    string botResponse = "You have already received a unique key.";
                    await botClient.SendTextMessageAsync(update.Message.Chat.Id, botResponse);
                }
                else
                {
                    // Generate a unique key
                    string uniqueKey = Guid.NewGuid().ToString();

                    // Save the key in the database
                    await SaveUserKey(update.Message.From.Id, uniqueKey);

                    // Prepare the bot's response
                    string botResponse = $"Your unique key: {uniqueKey}";
                    // Send the response to the user
                    await botClient.SendTextMessageAsync(update.Message.Chat.Id, botResponse);
                }
            }
        }

    }
    private static async Task<bool> UserHasKey(long userId)
    {
        using (var connection = new SqliteConnection(connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM Users WHERE UserId = @userId";
            command.Parameters.AddWithValue("@userId", userId);

            var count = (long)(await command.ExecuteScalarAsync());
            return count > 0; // Key already issued
        }
    }

    private static async Task SaveUserKey(long userId, string uniqueKey)
    {
        using (var connection = new SqliteConnection(connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO Users (UserId, UniqueKey) VALUES (@userId, @uniqueKey)";
            command.Parameters.AddWithValue("@userId", userId);
            command.Parameters.AddWithValue("@uniqueKey", uniqueKey);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Error: {exception.Message}");
        return Task.CompletedTask;
    }
}