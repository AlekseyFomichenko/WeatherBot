using System.Globalization;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

TelegramBotClient tgBotClient = new TelegramBotClient("my token");

using CancellationTokenSource cts = new();

ReceiverOptions? receiverOption = new ReceiverOptions()
{
    AllowedUpdates = Array.Empty<UpdateType>()
};

tgBotClient.StartReceiving(UpdateHandlerAsync,
                           PollingErrorHandlerAsync,
                           receiverOption,
                           cts.Token);

Console.WriteLine($"Start Weather Telegram Bot");
Console.ReadLine();

cts.Cancel();

async Task UpdateHandlerAsync(ITelegramBotClient botClient, Update update, CancellationToken token)
{
    if (update.Message is not { } message) return;
    if (message.Text is not { } messageText) return;

    Console.WriteLine($"Receiver message: '{messageText}'");

    try
    {
        string? msg = await GetTemperature(messageText);
        await botClient.SendMessage(
            chatId: message.Chat.Id,
            text: msg,
            cancellationToken: token);
    }
    catch
    {
        string? msg = $"Ошибка получения температуры города: {messageText}";

        await botClient.SendMessage(
            chatId: message.Chat.Id,
            text: msg,
            cancellationToken: token);
    }
}

async Task<(double Lt, double Ln)> GetGeoCoords(string cityName)
{
    HttpClient? client = new HttpClient();

    HttpResponseMessage? httpResp = await client.GetAsync(
        $"https://geocoding-api.open-meteo.com/v1/search?name={cityName}&count=1&language=en&format=json");

    string? content = await httpResp.Content.ReadAsStringAsync();

    Dictionary<string, object>? data = JsonSerializer.Deserialize<Dictionary<string, object>>(content);

    if (data.ContainsKey("results") is false)
        throw new Exception($"Unknown city: {cityName}");

    List<Dictionary<string, object>>? cityInfo = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(data["results"].ToString());

    bool isLatitude = double.TryParse(cityInfo[0]["latitude"].ToString(),  CultureInfo.InvariantCulture, out double lt);
    bool isLongitude = double.TryParse(cityInfo[0]["longitude"].ToString(), CultureInfo.InvariantCulture, out double ln);

    return isLatitude && isLongitude ? (lt, ln) : throw new ArgumentNullException($"Coordinates not found");
}
async Task<string> GetTemperature(string cityName)
{
    (double lt, double ln) = await GetGeoCoords(cityName);

    string ltStr = lt.ToString("0.00", CultureInfo.InvariantCulture);
    string lnStr = ln.ToString("0.00", CultureInfo.InvariantCulture);

    HttpClient client = new HttpClient();

    string? respStr = $"https://api.open-meteo.com/v1/forecast?latitude={ltStr}&longitude={lnStr}&current=temperature_2m&hourly=temperature_2m";
    HttpResponseMessage? httpResp = await client.GetAsync(respStr);

    string content = await httpResp.Content.ReadAsStringAsync();

    string? currData = JsonSerializer.Deserialize<Dictionary<string, object>>(content)?["current"].ToString();
    string? temp = JsonSerializer.Deserialize<Dictionary<string, object>>(currData)?["temperature_2m"].ToString();

    return $"{temp} °C";
}
Task PollingErrorHandlerAsync(ITelegramBotClient botClient, Exception exception, CancellationToken token)
{
    Console.WriteLine(exception.Message);
    return Task.CompletedTask;
}
