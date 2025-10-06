Imports TwitchLib.Client
Imports TwitchLib.Client.Events
Imports TwitchLib.Client.Models
Imports TwitchLib.Communication.Clients
Imports TwitchLib.Communication.Models
Imports Spectre.Console
Imports TwitchChatBot.Commands
Imports System.IO

''' <summary>
''' Main Twitch bot class that handles connection, authentication, and command routing.
''' Connects to Twitch chat using TwitchLib and processes incoming messages through the CommandRouter.
''' </summary>
Public Class Bot
    Private client As TwitchClient
    Private ReadOnly username As String
    Private ReadOnly oauth As String
    Private ReadOnly channel As String
    Private ReadOnly router As CommandRouter
    Private ReadOnly scheduler As New Scheduler()
    Private ReadOnly pluginManager As PluginManager
    Private sdk As BotSDK

    Public Sub New(username As String, oauth As String, channel As String)
        Me.username = username
        Me.oauth = oauth
        Me.channel = channel
        router = New CommandRouter("!")
        pluginManager = New PluginManager(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins"))

        Dim initPanel As New Panel("[green]Initializing bot components...[/]")
        initPanel.Header = New PanelHeader("[green]⚙ Bot Initialization[/]", Justify.Left)
        initPanel.Border = BoxBorder.Rounded
        initPanel.BorderColor(Color.Green)
        AnsiConsole.Write(initPanel)

        InitializeClient()
        RegisterBuiltInCommands()
        RegisterScheduledJobs()
        InitializePluginSystem()
    End Sub

    Private Sub InitializeClient()
        Dim credentials As New ConnectionCredentials(username, oauth)
        Dim clientOptions As New ClientOptions With {
            .MessagesAllowedInPeriod = 750,
            .ThrottlingPeriod = TimeSpan.FromSeconds(30)
        }

        Dim customClient As New WebSocketClient(clientOptions)
        client = New TwitchClient(customClient)

        client.Initialize(credentials, channel)

        AddHandler client.OnConnected, AddressOf OnConnected
        AddHandler client.OnJoinedChannel, AddressOf OnJoinedChannel
        AddHandler client.OnMessageReceived, AddressOf OnMessageReceived
        AddHandler client.OnConnectionError, AddressOf OnConnectionError

        AnsiConsole.MarkupLine("  [dim]└─[/] [yellow]Connecting to Twitch IRC...[/]")
    End Sub

    Public Sub Connect()
        client.Connect()
    End Sub

    Public Sub Disconnect()
        If client IsNot Nothing AndAlso client.IsConnected Then
            AnsiConsole.WriteLine()
            Dim shutdownPanel As New Panel("[yellow]Shutting down bot components...[/]")
            shutdownPanel.Header = New PanelHeader("[yellow]⏹ Shutdown[/]", Justify.Left)
            shutdownPanel.Border = BoxBorder.Rounded
            shutdownPanel.BorderColor(Color.Yellow)
            AnsiConsole.Write(shutdownPanel)

            pluginManager.ShutdownPlugins()
            scheduler.StopScheduler()
            client.Disconnect()

            AnsiConsole.MarkupLine("  [dim]└─[/] [red]Disconnected from Twitch[/]")
            AnsiConsole.WriteLine()
        End If
    End Sub

    Private Sub OnConnected(sender As Object, e As OnConnectedArgs)
        Dim connPanel As New Panel($"[green]Connected as[/] [bold cyan]{e.BotUsername}[/]")
        connPanel.Header = New PanelHeader("[green]✓ Twitch Connection[/]", Justify.Left)
        connPanel.Border = BoxBorder.Rounded
        connPanel.BorderColor(Color.Green)
        AnsiConsole.Write(connPanel)
    End Sub

    Private Sub OnJoinedChannel(sender As Object, e As OnJoinedChannelArgs)
        AnsiConsole.MarkupLine($"  [dim]└─[/] [cyan]Joined channel:[/] [bold]#{e.Channel}[/]")

        client.SendMessage(e.Channel, "🤖 Bot has been initialized and is now online!")

        AnsiConsole.MarkupLine("  [dim]└─[/] [green]Sent initialization message to chat[/]")

        scheduler.Start(client, e.Channel)

        LoadPlugins()
    End Sub

    Private Sub OnMessageReceived(sender As Object, e As OnMessageReceivedArgs)
        router.ProcessMessage(client, e)
    End Sub

    Private Sub OnConnectionError(sender As Object, e As OnConnectionErrorArgs)
        Dim errorPanel As New Panel($"[red]{e.Error.Message.EscapeMarkup()}[/]")
        errorPanel.Header = New PanelHeader("[red]✗ Connection Error[/]", Justify.Left)
        errorPanel.Border = BoxBorder.Rounded
        errorPanel.BorderColor(Color.Red)
        AnsiConsole.Write(errorPanel)
    End Sub

    Private Sub RegisterBuiltInCommands()
        router.RegisterCommand("ping", New PingCommand())
    End Sub

    Private Sub RegisterScheduledJobs()

    End Sub

    Private Sub InitializePluginSystem()
        sdk = New BotSDK(client, router, New PermissionService(), New CooldownService(), scheduler, channel)
        pluginManager.Initialize(sdk)
    End Sub

    Private Sub LoadPlugins()
        pluginManager.LoadPlugins()
    End Sub
End Class
