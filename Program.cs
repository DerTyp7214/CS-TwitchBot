using System.Drawing;
using System.Globalization;
using System.Net.Sockets;
using System.Text.RegularExpressions;

public static class StringExtensions
{
    public static string RemovePrefix(this string s, string prefix)
    {
        return Regex.Replace(s, "^" + prefix, String.Empty);
    }
}

class Program
{
    private TcpClient? twitchClient;

    private StreamReader? reader;
    private StreamWriter? writer;

    private string channel;

    static void Main(string[] args)
    {
        string channel = args[0];
        new Program(channel).Connect();
    }

    Program(string channel)
    {
        this.channel = channel;
    }

    private void Connect()
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
            if (line != null)
            {
                HandleLine(line);
            }
        }
    }

    private string GetEmoteUrl(string emoteId, string size = "4")
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
            emoteDict.Add(emoteParts[0], emoteParts[1].Split(','));
        }

        return emoteDict;
    }

    private (string, Dictionary<string, string[]>) FilterEmotes(string line, Dictionary<string, string[]>? emotes)
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
                OnMessage(tags, channel, username, message);
                break;
        }
    }

    private void OnMessage(Dictionary<string, Object> tags, string channel, string username, string message)
    {

        (string filteredMessage, Dictionary<string, string[]>? emotes) = FilterEmotes(message, (Dictionary<string, string[]>)tags["emotes-parsed"]);

        Console.WriteLine($"{tags["display-name"]}: {filteredMessage}");

        if (tags["emotes-parsed"] != null)
        {
            foreach (var emote in emotes)
            {
                foreach (var emotePos in emote.Value)
                {
                    string[] pos = emotePos.Split('-');
                    int start = Int32.Parse(pos[0]);
                    int end = Int32.Parse(pos[1]);
                    string emoteId = emote.Key;
                    string emoteUrl = GetEmoteUrl(emoteId);

                    Console.WriteLine($"Emote: {emoteUrl} at {start} - {end}");
                }
            }
        }
    }
}