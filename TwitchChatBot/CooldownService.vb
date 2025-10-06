Imports System.Collections.Concurrent
Imports Spectre.Console

''' <summary>
''' Thread-safe service for managing command cooldowns on per-user and global basis.
''' Tracks command usage timestamps and prevents spam by enforcing configurable delays.
''' </summary>
Public Class CooldownService
    Private ReadOnly cooldowns As New ConcurrentDictionary(Of String, DateTime)
    Private ReadOnly globalCooldowns As New ConcurrentDictionary(Of String, DateTime)

    Public Function IsOnCooldown(username As String, commandName As String, cooldownSeconds As Integer) As Boolean
        If cooldownSeconds <= 0 Then Return False

        Dim key As String = $"{username}:{commandName}".ToLowerInvariant()
        Dim now As DateTime = DateTime.UtcNow
        Dim lastUsed As DateTime

        If cooldowns.TryGetValue(key, lastUsed) Then
            Dim timeRemaining = lastUsed.AddSeconds(cooldownSeconds) - now
            If timeRemaining.TotalSeconds > 0 Then
                Return True
            End If
        End If

        Return False
    End Function

    Public Function IsOnGlobalCooldown(commandName As String, cooldownSeconds As Integer) As Boolean
        If cooldownSeconds <= 0 Then Return False

        Dim key As String = $"global:{commandName}".ToLowerInvariant()
        Dim now As DateTime = DateTime.UtcNow
        Dim lastUsed As DateTime

        If globalCooldowns.TryGetValue(key, lastUsed) Then
            Dim timeRemaining = lastUsed.AddSeconds(cooldownSeconds) - now
            If timeRemaining.TotalSeconds > 0 Then
                Return True
            End If
        End If

        Return False
    End Function

    Public Sub SetCooldown(username As String, commandName As String)
        Dim key As String = $"{username}:{commandName}".ToLowerInvariant()
        cooldowns(key) = DateTime.UtcNow
    End Sub

    Public Sub SetGlobalCooldown(commandName As String)
        Dim key As String = $"global:{commandName}".ToLowerInvariant()
        globalCooldowns(key) = DateTime.UtcNow
    End Sub

    Public Function GetRemainingCooldown(username As String, commandName As String, cooldownSeconds As Integer) As TimeSpan
        If cooldownSeconds <= 0 Then Return TimeSpan.Zero

        Dim key As String = $"{username}:{commandName}".ToLowerInvariant()
        Dim now As DateTime = DateTime.UtcNow
        Dim lastUsed As DateTime

        If cooldowns.TryGetValue(key, lastUsed) Then
            Dim timeRemaining = lastUsed.AddSeconds(cooldownSeconds) - now
            If timeRemaining.TotalSeconds > 0 Then
                Return timeRemaining
            End If
        End If

        Return TimeSpan.Zero
    End Function

    Public Function GetRemainingGlobalCooldown(commandName As String, cooldownSeconds As Integer) As TimeSpan
        If cooldownSeconds <= 0 Then Return TimeSpan.Zero

        Dim key As String = $"global:{commandName}".ToLowerInvariant()
        Dim now As DateTime = DateTime.UtcNow
        Dim lastUsed As DateTime

        If globalCooldowns.TryGetValue(key, lastUsed) Then
            Dim timeRemaining = lastUsed.AddSeconds(cooldownSeconds) - now
            If timeRemaining.TotalSeconds > 0 Then
                Return timeRemaining
            End If
        End If

        Return TimeSpan.Zero
    End Function

    Public Sub LogCooldownDenied(username As String, commandName As String, remainingTime As TimeSpan)
        AnsiConsole.MarkupLine($"[dim]→[/] [yellow]Cooldown:[/] {commandName.EscapeMarkup()} - {username.EscapeMarkup()} [dim]({remainingTime.TotalSeconds:F0}s remaining)[/]")
    End Sub

    Public Sub CleanupExpiredCooldowns()
        Dim now As DateTime = DateTime.UtcNow
        Dim keysToRemove As New List(Of String)

        For Each kvp In cooldowns
            Dim lastUsed As DateTime = kvp.Value
            If (now - lastUsed).TotalMinutes > 60 Then
                keysToRemove.Add(kvp.Key)
            End If
        Next

        For Each key In keysToRemove
            cooldowns.TryRemove(key, Nothing)
        Next

        keysToRemove.Clear()

        For Each kvp In globalCooldowns
            Dim lastUsed As DateTime = kvp.Value
            If (now - lastUsed).TotalMinutes > 60 Then
                keysToRemove.Add(kvp.Key)
            End If
        Next

        For Each key In keysToRemove
            globalCooldowns.TryRemove(key, Nothing)
        Next
    End Sub
End Class