class Program
{
    static void Main(string[] args)
    {
        string channel = args[0];

        TwitchBot bot = new TwitchBot(channel);

        bot.OnMessageReceived += OnMessageReceived;

        bot.Connect();
    }

    static private void OnMessageReceived(string message, string sender, string channel, Dictionary<string, object> tags)
    {
        Console.WriteLine($"{sender}: {message}");
    }
}