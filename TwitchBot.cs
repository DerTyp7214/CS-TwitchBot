using System.Net.Sockets;
using System.Text.RegularExpressions;

public static class StringExtensions
{
    public static string RemovePrefix(this string s, string prefix)
    {
        return Regex.Replace(s, "^" + prefix, String.Empty);
    }
}

public delegate void OnMessageReceived(string message, string sender, string channel, Dictionary<string, Object> tags);

public class TwitchBot
{
    private TcpClient? twitchClient;

    private StreamReader? reader;
    private StreamWriter? writer;

    private string channel;

    public event OnMessageReceived? OnMessageReceived;

    public TwitchBot(string channel)
    {
        this.channel = channel;
    }

    public void Connect()
    {
        twitchClient = new TcpClient("irc.chat.twitch.tv", 6667);

        reader = new StreamReader(twitchClient.GetStream());
        writer = new StreamWriter(twitchClient.GetStream())
        {
            NewLine = "\r\n",
            AutoFlush = true
        };

        writer.WriteLine("CAP REQ :twitch.tv/tags twitch.tv/commands twitch.tv/membership");
        writer.WriteLine("NICK justinfan123");

        writer.WriteLine($"JOIN #{channel}");

        writer.Flush();

        while (true)
        {
            string? line = reader.ReadLine();
            if (line != null) HandleLine(line);
        }
    }

    public string GetEmoteUrl(string emoteId, string size = "4")
    {
        return $"https://static-cdn.jtvnw.net/emoticons/v2/{emoteId}/default/dark/{size}.0";
    }

    private Dictionary<string, string[]> ParseEmotes(string emotes)
    {
        Dictionary<string, string[]> emoteDict = new Dictionary<string, string[]>();

        string[] emoteArray = emotes.Split('/');
        foreach (string emote in emoteArray)
        {
            string[] emoteParts = emote.Split(':');
            if (emoteParts.Length > 1)
                emoteDict.Add(emoteParts[0], emoteParts[1].Split(','));
        }

        return emoteDict;
    }

    public (string, Dictionary<string, string[]>) FilterEmotes(string line, Dictionary<string, string[]>? emotes)
    {
        string filteredLine = line;
        Dictionary<string, string[]> filteredEmotes = new Dictionary<string, string[]>();

        if (emotes == null) return (filteredLine, filteredEmotes);

        foreach (KeyValuePair<string, string[]> emote in emotes)
        {
            string emoteId = emote.Key;
            string[] emoteIndices = emote.Value;

            foreach (string emoteIndex in emoteIndices)
            {
                string[] indices = emoteIndex.Split('-');
                int startIndex = int.Parse(indices[0]);
                int endIndex = int.Parse(indices[1]);

                string emoteText = line.Substring(startIndex, endIndex - startIndex + 1);
                filteredLine = filteredLine.Replace(emoteText, emoteText.RemovePrefix(":").RemovePrefix(" "));
            }

            filteredEmotes.Add(emoteId, emoteIndices);
        }

        return (filteredLine, filteredEmotes);
    }

    private void HandleLine(string line)
    {
        if (writer == null)
        {
            return;
        }

        Dictionary<string, Object> tags = new Dictionary<string, Object>();

        foreach (var tag in line.RemovePrefix("@").Split(';'))
        {
            var split = tag.Split('=');
            tags.Add(split[0], split.Length > 1 ? split[1].Split(" ")[0] : "");

            if (split[0] == "emotes" && split.Length > 1)
            {
                tags["emotes-parsed"] = ParseEmotes(split[1]);
            }
        }

        string? command = line.Split("tmi.twitch.tv ").Length > 1 ? line.Split("tmi.twitch.tv ")[1].Split(" ")[0] : null;
        string prefix = new Regex(@" (:.*?!\w+@\w+\.tmi\.twitch\.tv) PRIVMSG #\w+ :").Match(line).Groups[1].Value;
        string username = prefix.Split("!")[0].RemovePrefix(":");

        switch (command)
        {
            case "PING":
                writer.WriteLine("PONG");
                break;
            case "PRIVMSG":
                string message = line.Split($"PRIVMSG #{channel} :")[1];
                OnMessageReceived?.Invoke(message, username, channel, tags);
                break;
        }
    }

    public void Ping()
    {
        writer?.WriteLine("PING");
    }

    public void SendMessage(string message)
    {
        writer?.WriteLine($"PRIVMSG #{channel} :{message}");
    }

    public void SendRawMessage(string rawMessage)
    {
        writer?.WriteLine(rawMessage);
    }

    public void Reconnect()
    {
        twitchClient?.Close();

        try
        {
            Connect();
        }
        catch (Exception)
        {
            Task.Delay(5000).ContinueWith((t) => Reconnect());
        }
    }
}