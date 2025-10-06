Imports TwitchLib.Client.Events
Imports TwitchLib.Client
Imports Spectre.Console
Imports System.Collections.Generic
Imports System.Runtime.CompilerServices

''' <summary>
''' Routes and executes chat commands with permission and cooldown validation.
''' Manages command registration and handles message parsing to execute appropriate commands.
''' </summary>
Public Class CommandRouter
    Private ReadOnly commands As New Dictionary(Of String, ICommand)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly commandPrefix As String
    Private ReadOnly permissionService As New PermissionService()
    Private ReadOnly cooldownService As New CooldownService()

    Public Sub New(Optional prefix As String = "!")
        commandPrefix = If(String.IsNullOrWhiteSpace(prefix), "!", prefix)
        AnsiConsole.MarkupLine("  [dim]└─[/] [blue]Command Router ready[/]")
    End Sub

    Public Sub RegisterCommand(commandName As String, command As ICommand)
        If String.IsNullOrWhiteSpace(commandName) OrElse command Is Nothing Then Return
        commands(commandName.Trim().ToLowerInvariant()) = command
        AnsiConsole.MarkupLine($"  [dim]└─[/] [green]Registered:[/] [cyan]{commandPrefix}{commandName.EscapeMarkup()}[/]")
    End Sub

    Public Sub UnregisterCommand(commandName As String)
        If String.IsNullOrWhiteSpace(commandName) Then Return
        If commands.Remove(commandName.Trim().ToLowerInvariant()) Then
            AnsiConsole.MarkupLine($"  [dim]└─[/] [yellow]Unregistered:[/] [cyan]{commandPrefix}{commandName.EscapeMarkup()}[/]")
        End If
    End Sub

    Public Sub ProcessMessage(client As TwitchClient, e As OnMessageReceivedArgs)
        Dim message As String = e.ChatMessage.Message
        If String.IsNullOrWhiteSpace(message) Then Return

        message = message.Trim()
        If Not message.StartsWith(commandPrefix) Then Return

        Dim withoutPrefix As String = message.Substring(commandPrefix.Length)
        If String.IsNullOrWhiteSpace(withoutPrefix) Then Return

        Dim commandParts As String() = withoutPrefix.Split({" "c}, StringSplitOptions.RemoveEmptyEntries)
        Dim commandName As String = commandParts(0).ToLowerInvariant()

        Dim handler As ICommand = Nothing
        If commands.TryGetValue(commandName, handler) Then
            Try
                If Not permissionService.HasRole(e, handler.RequiredRole) Then
                    permissionService.LogPermissionDenied(e.ChatMessage.DisplayName, commandPrefix & commandName,
                                                        permissionService.GetUserRole(e), handler.RequiredRole)
                    Return
                End If

                Dim username As String = e.ChatMessage.Username
                Dim userCooldown As Integer = handler.UserCooldownSeconds
                Dim globalCooldown As Integer = handler.GlobalCooldownSeconds

                If cooldownService.IsOnCooldown(username, commandName, userCooldown) Then
                    Dim remaining = cooldownService.GetRemainingCooldown(username, commandName, userCooldown)
                    cooldownService.LogCooldownDenied(username, commandPrefix & commandName, remaining)
                    Return
                End If

                If cooldownService.IsOnGlobalCooldown(commandName, globalCooldown) Then
                    Dim remaining = cooldownService.GetRemainingGlobalCooldown(commandName, globalCooldown)
                    cooldownService.LogCooldownDenied("[GLOBAL]", commandPrefix & commandName, remaining)
                    Return
                End If

                Dim args As String() = If(commandParts.Length > 1, commandParts.Skip(1).ToArray(), Array.Empty(Of String)())
                handler.Execute(client, e, args)

                If userCooldown > 0 Then cooldownService.SetCooldown(username, commandName)
                If globalCooldown > 0 Then cooldownService.SetGlobalCooldown(commandName)

                AnsiConsole.MarkupLine($"[dim]→ {commandPrefix}{commandName.EscapeMarkup()} by {e.ChatMessage.DisplayName.EscapeMarkup()}[/]")
            Catch ex As Exception
                AnsiConsole.MarkupLine($"[red]✗ Command error ({commandPrefix}{commandName.EscapeMarkup()}): {ex.Message.EscapeMarkup()}[/]")
            End Try
        End If
    End Sub

    Public Function GetRegisteredCommands() As String()
        Return commands.Keys.OrderBy(Function(k) k).ToArray()
    End Function
End Class

''' <summary>
''' Interface for bot commands that defines role requirements, cooldowns, and execution method.
''' </summary>
Public Interface ICommand
    ''' <summary>Minimum role required to execute this command</summary>
    ReadOnly Property RequiredRole As Role
    ''' <summary>Per-user cooldown in seconds (0 = no cooldown)</summary>
    ReadOnly Property UserCooldownSeconds As Integer
    ''' <summary>Global cooldown in seconds (0 = no cooldown)</summary>
    ReadOnly Property GlobalCooldownSeconds As Integer
    ''' <summary>Execute the command with given parameters</summary>
    Sub Execute(client As TwitchClient, e As OnMessageReceivedArgs, args As String())
End Interface
