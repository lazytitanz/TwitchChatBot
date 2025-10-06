Imports TwitchLib.Client
Imports Spectre.Console

''' <summary>
''' SDK that exposes bot services to plugins for command registration and bot interaction.
''' Provides controlled access to core bot functionality while maintaining security.
''' </summary>
Public Class BotSDK
    Private ReadOnly client As TwitchClient
    Private ReadOnly commandRouter As CommandRouter
    Private ReadOnly permissionService As PermissionService
    Private ReadOnly cooldownService As CooldownService
    Private ReadOnly scheduler As Scheduler
    Private ReadOnly botChannel As String
    Private _pluginFolder As String

    Public Sub New(client As TwitchClient, router As CommandRouter, permissions As PermissionService,
                   cooldowns As CooldownService, scheduler As Scheduler, channel As String)
        Me.client = client
        Me.commandRouter = router
        Me.permissionService = permissions
        Me.cooldownService = cooldowns
        Me.scheduler = scheduler
        Me.botChannel = channel
    End Sub

    ''' <summary>
    ''' Register a new command with the bot
    ''' </summary>
    Public Sub RegisterCommand(name As String, command As ICommand)
        commandRouter.RegisterCommand(name, command)
    End Sub

    ''' <summary>
    ''' Unregister a command from the bot
    ''' </summary>
    Public Sub UnregisterCommand(name As String)
        commandRouter.UnregisterCommand(name)
    End Sub

    ''' <summary>
    ''' Schedule a recurring job
    ''' </summary>
    Public Sub ScheduleJob(name As String, intervalMinutes As Integer, command As IScheduledCommand)
        scheduler.ScheduleJob(name, intervalMinutes, command)
    End Sub

    ''' <summary>
    ''' Remove a scheduled job
    ''' </summary>
    Public Sub RemoveScheduledJob(name As String)
        scheduler.RemoveJob(name)
    End Sub

    ''' <summary>
    ''' Send a message to the bot's channel
    ''' </summary>
    Public Sub SendMessage(message As String)
        client.SendMessage(botChannel, message)
    End Sub

    ''' <summary>
    ''' Get access to permission service for role checking
    ''' </summary>
    Public ReadOnly Property Permissions As PermissionService
        Get
            Return permissionService
        End Get
    End Property

    ''' <summary>
    ''' Get access to cooldown service for timing controls
    ''' </summary>
    Public ReadOnly Property Cooldowns As CooldownService
        Get
            Return cooldownService
        End Get
    End Property

    ''' <summary>
    ''' Get the bot's current channel
    ''' </summary>
    Public ReadOnly Property Channel As String
        Get
            Return botChannel
        End Get
    End Property

    ''' <summary>
    ''' Log a message to the console with plugin context
    ''' </summary>
    Public Sub LogInfo(pluginName As String, message As String)
        AnsiConsole.MarkupLine($"  [dim]└─[/] [blue]Plugin:[/] [cyan]{pluginName.EscapeMarkup()}[/] [dim]→[/] {message.EscapeMarkup()}")
    End Sub

    ''' <summary>
    ''' Log an error message to the console with plugin context
    ''' </summary>
    Public Sub LogError(pluginName As String, message As String)
        AnsiConsole.MarkupLine($"  [red]✗ Plugin:[/] [cyan]{pluginName.EscapeMarkup()}[/] [dim]→[/] [red]{message.EscapeMarkup()}[/]")
    End Sub

    ''' <summary>
    ''' Set the plugin folder path (called by PluginManager before Initialize)
    ''' </summary>
    Friend Sub SetPluginFolder(folderPath As String)
        _pluginFolder = folderPath
    End Sub

    ''' <summary>
    ''' Get the plugin's folder path for loading configuration and resources
    ''' </summary>
    Public ReadOnly Property PluginFolder As String
        Get
            Return _pluginFolder
        End Get
    End Property
End Class