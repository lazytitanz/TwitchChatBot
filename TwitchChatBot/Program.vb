Imports System
Imports System.IO
Imports Microsoft.Extensions.Configuration
Imports Spectre.Console

Module Program
    Sub Main(args As String())
        ' Display startup banner
        DisplayBanner()

        ' Load configuration from appsettings.json
        Dim config = New ConfigurationBuilder() _
            .SetBasePath(Directory.GetCurrentDirectory()) _
            .AddJsonFile("appsettings.json", optional:=False, reloadOnChange:=False) _
            .Build()

        Dim username = config("Bot:Username")
        Dim oauthToken = config("Bot:OAuthToken")
        Dim channel = config("Bot:Channel")

        ' Validate configuration
        If String.IsNullOrWhiteSpace(username) OrElse String.IsNullOrWhiteSpace(oauthToken) OrElse String.IsNullOrWhiteSpace(channel) Then
            Dim errorPanel As New Panel("[red]Configuration Error[/]" & Environment.NewLine &
                "Bot configuration is incomplete in appsettings.json")
            errorPanel.Header = New PanelHeader("[red]✗ Startup Failed[/]")
            errorPanel.BorderColor(Color.Red)
            AnsiConsole.Write(errorPanel)
            Return
        End If

        Dim bot As New Bot(username, oauthToken, channel)
        bot.Connect()

        AnsiConsole.WriteLine()
        AnsiConsole.Write(New Panel("[dim]Press any key to stop the bot...[/]") _
            .BorderColor(Color.Grey) _
            .RoundedBorder())
        Console.ReadKey()

        AnsiConsole.WriteLine()
        bot.Disconnect()
    End Sub

    Private Sub DisplayBanner()
        Dim banner As New Panel(
            "[bold purple]TwitchChatBot[/]" & Environment.NewLine &
            "[dim]A Plugin-based Twitch chat bot.[/]" & Environment.NewLine &
            Environment.NewLine &
            "[grey]Version:[/] [cyan]1.0.0[/] | [grey].NET:[/] [cyan]8.0[/]"
        )
        banner.Header = New PanelHeader("[purple]╔══════════════════════════════════╗[/]", Justify.Center)
        banner.Border = BoxBorder.Rounded
        banner.BorderColor(Color.Purple)
        banner.Expand = False

        AnsiConsole.Write(banner)
        AnsiConsole.WriteLine()
    End Sub
End Module
